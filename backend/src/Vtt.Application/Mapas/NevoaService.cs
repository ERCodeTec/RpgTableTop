using Microsoft.EntityFrameworkCore;
using Vtt.Application.Common;
using Vtt.Application.Mesas;
using Vtt.Domain.Mapas;
using Vtt.Domain.Mesas;

namespace Vtt.Application.Mapas;

public class NevoaService(IAppDbContext db, IMapaNotifier notifier) : INevoaService
{
    /// <summary>Duas gravações na mesma camada disputam a versão; quem perde relê e tenta de novo.</summary>
    private const int Tentativas = 3;

    private const string CamadaInvalida = "A camada não corresponde às dimensões do mapa.";

    public async Task<Resultado<MapaDto>> DefinirModoAsync(Guid usuarioId, Guid mapaId, NevoaRequest request, CancellationToken cancellationToken = default)
    {
        var (mapa, erro) = await ObterComoMestreAsync(usuarioId, mapaId, cancellationToken);
        if (mapa is null)
        {
            return erro!;
        }

        mapa.Nevoa = request.Modo!.Value;
        await db.SaveChangesAsync(cancellationToken);

        var dto = MapaDto.De(mapa);
        await notifier.MapaAtualizadoAsync(AlcanceDoMapa.De(mapa, usuarioId), dto);
        return dto;
    }

    public async Task<Resultado> ExplorarAsync(Guid usuarioId, Guid mapaId, byte[]? celulas, CancellationToken cancellationToken = default)
    {
        var mapa = await db.Mapas.AsNoTracking().FirstOrDefaultAsync(m => m.Id == mapaId, cancellationToken);
        if (mapa is null)
        {
            return Erro.NaoEncontrado();
        }

        var papel = await db.ObterPapelAsync(usuarioId, mapa.MesaId, cancellationToken);
        if (papel is null || (papel == PapelNaMesa.Jogador && !mapa.Ativo))
        {
            return Erro.NaoEncontrado();
        }

        // O Mestre vê tudo e não explora; com a névoa desligada, não há memória a guardar.
        if (papel == PapelNaMesa.Mestre || mapa.Nevoa == ModoDeNevoa.Desligada)
        {
            return Resultado.Ok;
        }

        if (MapaDeBits.Normalizar(celulas, mapa.Celulas) is not { } vistas)
        {
            return Erro.Invalido("celulas", CamadaInvalida);
        }

        var dono = mapa.Nevoa == ModoDeNevoa.Individual ? usuarioId : (Guid?)null;
        var memoria = await AlterarAsync(mapa, TipoDeMemoria.Explorada, dono, bits => MapaDeBits.Unir(bits, vistas), cancellationToken);
        if (memoria is not null)
        {
            await notifier.MemoriasDeNevoaAlteradasAsync(AlcanceDoMapa.De(mapa, usuarioId), [MemoriaDeNevoaDto.De(memoria)]);
        }

        return Resultado.Ok;
    }

    public async Task<Resultado> RevelarAsync(Guid usuarioId, Guid mapaId, RevelarCelulasRequest request, CancellationToken cancellationToken = default)
    {
        var (mapa, erro) = await ObterComoMestreAsync(usuarioId, mapaId, cancellationToken);
        if (mapa is null)
        {
            return erro!;
        }

        if (MapaDeBits.Normalizar(request.Celulas, mapa.Celulas) is not { } pintadas)
        {
            return Erro.Invalido("celulas", CamadaInvalida);
        }

        IReadOnlyList<MemoriaDeNevoa> alteradas;
        if (request.Revelar)
        {
            var revelada = await AlterarAsync(mapa, TipoDeMemoria.Revelada, null, bits => MapaDeBits.Unir(bits, pintadas), cancellationToken);
            alteradas = revelada is null ? [] : [revelada];
        }
        else
        {
            alteradas = await AlterarTodasAsync(mapa.Id, memoria => MapaDeBits.Apagar(memoria.Celulas, pintadas), cancellationToken);
        }

        await AvisarAsync(mapa, usuarioId, alteradas);
        return Resultado.Ok;
    }

    public async Task<Resultado> LimparMemoriaAsync(Guid usuarioId, Guid mapaId, CancellationToken cancellationToken = default)
    {
        var (mapa, erro) = await ObterComoMestreAsync(usuarioId, mapaId, cancellationToken);
        if (mapa is null)
        {
            return erro!;
        }

        var alteradas = await AlterarTodasAsync(
            mapa.Id,
            memoria => memoria.Tipo == TipoDeMemoria.Explorada && MapaDeBits.Apagar(memoria.Celulas, memoria.Celulas.ToArray()),
            cancellationToken);
        await AvisarAsync(mapa, usuarioId, alteradas);
        return Resultado.Ok;
    }

    private async Task AvisarAsync(Mapa mapa, Guid mestreId, IReadOnlyList<MemoriaDeNevoa> alteradas)
    {
        if (alteradas.Count > 0)
        {
            await notifier.MemoriasDeNevoaAlteradasAsync(AlcanceDoMapa.De(mapa, mestreId), alteradas.Select(MemoriaDeNevoaDto.De).ToList());
        }
    }

    /// <summary>
    /// Lê (ou cria) uma camada, aplica <paramref name="alterar"/> sobre uma cópia dos bits e grava.
    /// Devolve a camada gravada, ou null se nada mudou.
    /// </summary>
    private async Task<MemoriaDeNevoa?> AlterarAsync(
        Mapa mapa, TipoDeMemoria tipo, Guid? usuarioId, Func<byte[], bool> alterar, CancellationToken cancellationToken)
    {
        for (var tentativa = 0; tentativa < Tentativas; tentativa++)
        {
            var memoria = await db.MemoriasDeNevoa.FirstOrDefaultAsync(
                m => m.MapaId == mapa.Id && m.Tipo == tipo && m.UsuarioId == usuarioId, cancellationToken);
            var nova = memoria is null;
            memoria ??= new MemoriaDeNevoa { Id = Guid.NewGuid(), MapaId = mapa.Id, Tipo = tipo, UsuarioId = usuarioId };

            var bits = MapaDeBits.Normalizar(memoria.Celulas, mapa.Celulas) ?? MapaDeBits.Vazio(mapa.Celulas);
            if (!alterar(bits))
            {
                return null;
            }

            // Um array novo: o EF compara byte[] por referência para saber o que gravar.
            memoria.Celulas = bits;
            if (nova)
            {
                db.MemoriasDeNevoa.Add(memoria);
            }
            else
            {
                memoria.Versao++;
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return memoria;
            }
            catch (DbUpdateException)
            {
                // Outra gravação na mesma camada (ou a criação dela) chegou antes.
                db.MemoriasDeNevoa.Entry(memoria).State = EntityState.Detached;
            }
        }

        return null;
    }

    /// <summary>Aplica <paramref name="alterar"/> (que muda os bits no lugar) a cada camada do mapa e grava as que mudaram.</summary>
    private async Task<IReadOnlyList<MemoriaDeNevoa>> AlterarTodasAsync(
        Guid mapaId, Func<MemoriaDeNevoa, bool> alterar, CancellationToken cancellationToken)
    {
        for (var tentativa = 0; tentativa < Tentativas; tentativa++)
        {
            var memorias = await db.MemoriasDeNevoa.Where(m => m.MapaId == mapaId).ToListAsync(cancellationToken);
            var alteradas = new List<MemoriaDeNevoa>();
            foreach (var memoria in memorias)
            {
                var antes = memoria.Celulas;
                memoria.Celulas = antes.ToArray();
                if (alterar(memoria))
                {
                    memoria.Versao++;
                    alteradas.Add(memoria);
                }
                else
                {
                    memoria.Celulas = antes;
                }
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return alteradas;
            }
            catch (DbUpdateConcurrencyException)
            {
                foreach (var memoria in memorias)
                {
                    db.MemoriasDeNevoa.Entry(memoria).State = EntityState.Detached;
                }
            }
        }

        return [];
    }

    private async Task<(Mapa? Mapa, Erro? Erro)> ObterComoMestreAsync(Guid usuarioId, Guid mapaId, CancellationToken cancellationToken)
    {
        var mapa = await db.Mapas.FirstOrDefaultAsync(m => m.Id == mapaId, cancellationToken);
        if (mapa is null)
        {
            return (null, Erro.NaoEncontrado());
        }

        var erro = await db.ExigirMestreAsync(usuarioId, mapa.MesaId, cancellationToken);
        return erro is null ? (mapa, null) : (null, erro);
    }
}
