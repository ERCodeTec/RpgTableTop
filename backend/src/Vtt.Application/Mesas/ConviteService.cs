using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vtt.Application.Common;
using Vtt.Application.Mapas;
using Vtt.Domain.Mesas;

namespace Vtt.Application.Mesas;

public class ConviteService(
    IAppDbContext db,
    IUsuarioDirectory usuarios,
    IMesaNotifier notifier,
    TokensDeJogador tokensDeJogador,
    ILinkPublico links,
    TimeProvider relogio,
    ILogger<ConviteService> logger) : IConviteService
{
    private const string ConviteInvalido = "Convite inválido ou não mais disponível.";

    public async Task<Resultado<ConviteDto>> GerarAsync(Guid usuarioId, Guid mesaId, ConviteRequest request, CancellationToken cancellationToken = default)
    {
        var erro = await db.ExigirMestreAsync(usuarioId, mesaId, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        var agora = relogio.GetUtcNow();
        var convite = new Convite
        {
            Id = Guid.NewGuid(),
            MesaId = mesaId,
            Codigo = await GerarCodigoUnicoAsync(cancellationToken),
            CriadoEm = agora,
            ExpiraEm = request.ValidadeHoras is { } horas ? agora.AddHours(horas) : null,
            UsosMaximos = request.UsosMaximos
        };

        db.Convites.Add(convite);
        await db.SaveChangesAsync(cancellationToken);

        return ParaDto(convite, agora);
    }

    public async Task<Resultado<IReadOnlyList<ConviteDto>>> ListarAsync(Guid usuarioId, Guid mesaId, CancellationToken cancellationToken = default)
    {
        var erro = await db.ExigirMestreAsync(usuarioId, mesaId, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        var convites = await db.Convites
            .AsNoTracking()
            .Where(convite => convite.MesaId == mesaId)
            .OrderByDescending(convite => convite.CriadoEm)
            .ToListAsync(cancellationToken);

        var agora = relogio.GetUtcNow();
        return convites.Select(convite => ParaDto(convite, agora)).ToList();
    }

    public async Task<Resultado> RevogarAsync(Guid usuarioId, string codigo, CancellationToken cancellationToken = default)
    {
        var convite = await BuscarAsync(codigo, cancellationToken);
        if (convite is null)
        {
            return Erro.NaoEncontrado();
        }

        var erro = await db.ExigirMestreAsync(usuarioId, convite.MesaId, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        if (convite.RevogadoEm is null)
        {
            convite.RevogadoEm = relogio.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
        }

        return Resultado.Ok;
    }

    public async Task<Resultado<ConvitePreviaDto>> PreVisualizarAsync(Guid usuarioId, string codigo, CancellationToken cancellationToken = default)
    {
        var convite = await BuscarAsync(codigo, cancellationToken);
        if (convite is null || !convite.EstaValido(relogio.GetUtcNow()))
        {
            return Erro.NaoEncontrado(ConviteInvalido);
        }

        var mesa = await db.Mesas
            .AsNoTracking()
            .Include(m => m.Membros)
            .FirstAsync(m => m.Id == convite.MesaId, cancellationToken);

        var mestreId = mesa.Membros.First(membro => membro.Papel == PapelNaMesa.Mestre).UsuarioId;
        var nomes = await usuarios.ObterNomesDeExibicaoAsync([mestreId], cancellationToken);

        return new ConvitePreviaDto(
            convite.Codigo,
            mesa.Id,
            mesa.Nome,
            mesa.Descricao,
            nomes.GetValueOrDefault(mestreId, string.Empty),
            mesa.Membros.Count(membro => membro.Papel == PapelNaMesa.Jogador),
            mesa.Membros.Any(membro => membro.UsuarioId == usuarioId));
    }

    public async Task<Resultado<AceitarConviteResponse>> AceitarAsync(Guid usuarioId, string codigo, Guid personagemId, CancellationToken cancellationToken = default)
    {
        var agora = relogio.GetUtcNow();
        var convite = await BuscarAsync(codigo, cancellationToken);
        if (convite is null || !convite.EstaValido(agora))
        {
            return Erro.NaoEncontrado(ConviteInvalido);
        }

        var personagem = await db.Personagens
            .FirstOrDefaultAsync(p => p.Id == personagemId && p.UsuarioId == usuarioId, cancellationToken);
        if (personagem is null)
        {
            return Erro.Invalido("personagemId", "Escolha um dos seus personagens.");
        }

        var mesaId = convite.MesaId;
        var membro = await db.MesaMembros
            .FirstOrDefaultAsync(m => m.MesaId == mesaId && m.UsuarioId == usuarioId, cancellationToken);

        if (membro is not null)
        {
            // Quem já é membro não consome o convite; no máximo troca de personagem, sob a mesma regra do lobby.
            if (membro.PersonagemAtivoId != personagem.Id)
            {
                var sessaoAtiva = await db.Mesas
                    .Where(mesa => mesa.Id == mesaId)
                    .Select(mesa => mesa.SessaoAtiva)
                    .FirstAsync(cancellationToken);
                if (membro.Papel == PapelNaMesa.Jogador && sessaoAtiva)
                {
                    return Erro.Conflito(
                        "Você já está nesta mesa e a sessão está ativa. Só o Mestre pode trocar seu personagem agora.");
                }

                membro.PersonagemAtivoId = personagem.Id;
                await db.SaveChangesAsync(cancellationToken);
                await notifier.PersonagemAtivoAlteradoAsync(mesaId, usuarioId, PersonagemResumoDto.ParaOLobby(personagem));
                await tokensDeJogador.GarantirAsync(mesaId, usuarioId, cancellationToken);
            }

            return new AceitarConviteResponse(mesaId);
        }

        var novoMembro = new MesaMembro
        {
            Id = Guid.NewGuid(),
            MesaId = mesaId,
            UsuarioId = usuarioId,
            Papel = PapelNaMesa.Jogador,
            PersonagemAtivoId = personagem.Id,
            PersonagemAtivo = personagem,
            EntrouEm = agora
        };

        await using (var transacao = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            // O uso é reservado com a condição dentro do UPDATE: se dois jogadores disputarem
            // a última vaga do convite ao mesmo tempo, só um deles altera a linha.
            var reservados = await db.Convites
                .Where(c => c.Id == convite.Id
                    && c.RevogadoEm == null
                    && (c.UsosMaximos == null || c.UsosAtuais < c.UsosMaximos))
                .ExecuteUpdateAsync(
                    c => c.SetProperty(x => x.UsosAtuais, x => x.UsosAtuais + 1), cancellationToken);
            if (reservados == 0)
            {
                return Erro.NaoEncontrado(ConviteInvalido);
            }

            db.MesaMembros.Add(novoMembro);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Índice único (mesa, usuário): o mesmo usuário aceitou duas vezes em paralelo.
                // A transação é desfeita ao sair do bloco e o uso reservado não é contado.
                return Erro.Conflito("Sua entrada nesta mesa já estava em andamento. Recarregue a página.");
            }

            await transacao.CommitAsync(cancellationToken);
        }

        logger.LogInformation("Usuário {UsuarioId} entrou na mesa {MesaId} por convite", usuarioId, mesaId);
        var nomes = await usuarios.ObterNomesDeExibicaoAsync([usuarioId], cancellationToken);
        await notifier.MembroEntrouAsync(mesaId, MembroDto.De(novoMembro, nomes.GetValueOrDefault(usuarioId, string.Empty)));
        await tokensDeJogador.GarantirAsync(mesaId, usuarioId, cancellationToken);

        return new AceitarConviteResponse(mesaId);
    }

    private Task<Convite?> BuscarAsync(string codigo, CancellationToken cancellationToken)
    {
        var normalizado = codigo.Trim().ToUpperInvariant();
        return db.Convites.FirstOrDefaultAsync(convite => convite.Codigo == normalizado, cancellationToken);
    }

    private async Task<string> GerarCodigoUnicoAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var codigo = RandomNumberGenerator.GetString(Convite.Alfabeto, Convite.CodigoTamanho);
            if (!await db.Convites.AnyAsync(convite => convite.Codigo == codigo, cancellationToken))
            {
                return codigo;
            }
        }
    }

    private ConviteDto ParaDto(Convite convite, DateTimeOffset agora) => new(
        convite.Codigo,
        links.Convite(convite.Codigo),
        convite.CriadoEm,
        convite.ExpiraEm,
        convite.RevogadoEm,
        convite.UsosMaximos,
        convite.UsosAtuais,
        convite.EstaValido(agora));
}
