using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vtt.Application.Common;
using Vtt.Application.Mapas;
using Vtt.Domain.Imagens;
using Vtt.Domain.Mesas;

namespace Vtt.Application.Mesas;

public class MesaService(
    IAppDbContext db,
    IUsuarioDirectory usuarios,
    IMesaNotifier notifier,
    TokensDeJogador tokensDeJogador,
    IFileStorage arquivos,
    TimeProvider relogio,
    ILogger<MesaService> logger) : IMesaService
{
    public async Task<MesaDetalheDto> CriarAsync(Guid usuarioId, MesaRequest request, CancellationToken cancellationToken = default)
    {
        var agora = relogio.GetUtcNow();
        var mesa = new Mesa
        {
            Id = Guid.NewGuid(),
            CriadaPorUsuarioId = usuarioId,
            CriadaEm = agora
        };
        Aplicar(mesa, request);

        // Mesa e Mestre vão no mesmo SaveChanges, ou seja, na mesma transação.
        mesa.Membros.Add(new MesaMembro
        {
            Id = Guid.NewGuid(),
            MesaId = mesa.Id,
            UsuarioId = usuarioId,
            Papel = PapelNaMesa.Mestre,
            EntrouEm = agora
        });

        db.Mesas.Add(mesa);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Mesa {MesaId} criada pelo usuário {UsuarioId}", mesa.Id, usuarioId);

        return await MontarDetalheAsync(mesa, usuarioId, cancellationToken);
    }

    public async Task<IReadOnlyList<MesaResumoDto>> ListarAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        return await db.MesaMembros
            .AsNoTracking()
            .Where(membro => membro.UsuarioId == usuarioId)
            .Join(db.Mesas, membro => membro.MesaId, mesa => mesa.Id, (membro, mesa) => new { membro.Papel, mesa })
            .OrderBy(x => x.mesa.Nome)
            .Select(x => new MesaResumoDto(
                x.mesa.Id,
                x.mesa.Nome,
                x.mesa.Descricao,
                x.mesa.SessaoAtiva,
                x.Papel,
                x.mesa.Membros.Count,
                x.mesa.CriadaEm))
            .ToListAsync(cancellationToken);
    }

    public async Task<Resultado<MesaDetalheDto>> ObterAsync(Guid usuarioId, Guid mesaId, CancellationToken cancellationToken = default)
    {
        var mesa = await db.Mesas
            .AsNoTracking()
            .Include(m => m.Membros).ThenInclude(membro => membro.PersonagemAtivo)
            .FirstOrDefaultAsync(m => m.Id == mesaId && m.Membros.Any(membro => membro.UsuarioId == usuarioId), cancellationToken);

        return mesa is null
            ? Erro.NaoEncontrado()
            : await MontarDetalheAsync(mesa, usuarioId, cancellationToken);
    }

    public Task<bool> EhMembroAsync(Guid usuarioId, Guid mesaId, CancellationToken cancellationToken = default) =>
        db.MesaMembros.AnyAsync(membro => membro.MesaId == mesaId && membro.UsuarioId == usuarioId, cancellationToken);

    public async Task<Resultado<MesaDetalheDto>> AtualizarAsync(Guid usuarioId, Guid mesaId, MesaRequest request, CancellationToken cancellationToken = default)
    {
        var (mesa, erro) = await ObterMesaComoMestreAsync(usuarioId, mesaId, cancellationToken);
        if (mesa is null)
        {
            return erro!;
        }

        Aplicar(mesa, request);
        await db.SaveChangesAsync(cancellationToken);

        return await MontarDetalheAsync(mesa, usuarioId, cancellationToken);
    }

    public async Task<Resultado> ExcluirAsync(Guid usuarioId, Guid mesaId, CancellationToken cancellationToken = default)
    {
        var (mesa, erro) = await ObterMesaComoMestreAsync(usuarioId, mesaId, cancellationToken);
        if (mesa is null)
        {
            return erro!;
        }

        // Membros, convites, mapas e imagens saem em cascata no banco; os arquivos das imagens, do disco.
        db.Mesas.Remove(mesa);
        await db.SaveChangesAsync(cancellationToken);
        await arquivos.ExcluirPastaAsync(ImagemUpload.PastaDaMesa(mesaId), cancellationToken);
        logger.LogInformation("Mesa {MesaId} excluída pelo usuário {UsuarioId}", mesaId, usuarioId);

        await notifier.MesaExcluidaAsync(mesaId);
        return Resultado.Ok;
    }

    public async Task<Resultado<MesaDetalheDto>> DefinirSessaoAsync(Guid usuarioId, Guid mesaId, bool ativa, CancellationToken cancellationToken = default)
    {
        var (mesa, erro) = await ObterMesaComoMestreAsync(usuarioId, mesaId, cancellationToken);
        if (mesa is null)
        {
            return erro!;
        }

        if (mesa.SessaoAtiva != ativa)
        {
            mesa.SessaoAtiva = ativa;
            await db.SaveChangesAsync(cancellationToken);
            await notifier.SessaoAlteradaAsync(mesaId, ativa);
        }

        return await MontarDetalheAsync(mesa, usuarioId, cancellationToken);
    }

    public async Task<Resultado<MembroDto>> DefinirMeuPersonagemAsync(Guid usuarioId, Guid mesaId, Guid personagemId, CancellationToken cancellationToken = default)
    {
        var membro = await ObterMembroAsync(mesaId, usuarioId, cancellationToken);
        if (membro is null)
        {
            return Erro.NaoEncontrado();
        }

        if (membro.Papel == PapelNaMesa.Jogador && membro.PersonagemAtivoId != personagemId)
        {
            var sessaoAtiva = await db.Mesas
                .Where(mesa => mesa.Id == mesaId)
                .Select(mesa => mesa.SessaoAtiva)
                .FirstAsync(cancellationToken);
            if (sessaoAtiva)
            {
                return Erro.Conflito(
                    "A sessão está ativa. Você só pode trocar de personagem quando o Mestre encerrar a sessão; durante ela, peça a troca ao Mestre.");
            }
        }

        return await TrocarPersonagemAsync(membro, personagemId, cancellationToken);
    }

    public async Task<Resultado<MembroDto>> DefinirPersonagemDoMembroAsync(Guid usuarioId, Guid mesaId, Guid membroUsuarioId, Guid personagemId, CancellationToken cancellationToken = default)
    {
        var erro = await db.ExigirMestreAsync(usuarioId, mesaId, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        var membro = await ObterMembroAsync(mesaId, membroUsuarioId, cancellationToken);
        return membro is null
            ? Erro.NaoEncontrado("Este jogador não está na mesa.")
            : await TrocarPersonagemAsync(membro, personagemId, cancellationToken);
    }

    public async Task<Resultado<IReadOnlyList<PersonagemResumoDto>>> ListarPersonagensDoMembroAsync(Guid usuarioId, Guid mesaId, Guid membroUsuarioId, CancellationToken cancellationToken = default)
    {
        var erro = await db.ExigirMestreAsync(usuarioId, mesaId, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        if (!await EhMembroAsync(membroUsuarioId, mesaId, cancellationToken))
        {
            return Erro.NaoEncontrado("Este jogador não está na mesa.");
        }

        var personagens = await db.Personagens
            .AsNoTracking()
            .Where(personagem => personagem.UsuarioId == membroUsuarioId)
            .OrderBy(personagem => personagem.Nome)
            .ToListAsync(cancellationToken);

        return personagens.Select(PersonagemResumoDto.De).ToList();
    }

    public async Task<Resultado> RemoverMembroAsync(Guid usuarioId, Guid mesaId, Guid membroUsuarioId, CancellationToken cancellationToken = default)
    {
        var solicitante = await ObterMembroAsync(mesaId, usuarioId, cancellationToken);
        if (solicitante is null)
        {
            return Erro.NaoEncontrado();
        }

        var saindoPorContaPropria = usuarioId == membroUsuarioId;
        if (saindoPorContaPropria)
        {
            if (solicitante.Papel == PapelNaMesa.Mestre)
            {
                return Erro.Conflito("O Mestre não pode sair da mesa. Para encerrar a mesa, exclua-a.");
            }

            db.MesaMembros.Remove(solicitante);
        }
        else
        {
            if (solicitante.Papel != PapelNaMesa.Mestre)
            {
                return Erro.Proibido(MesaAcesso.SomenteMestre);
            }

            var alvo = await ObterMembroAsync(mesaId, membroUsuarioId, cancellationToken);
            if (alvo is null)
            {
                return Erro.NaoEncontrado("Este jogador não está na mesa.");
            }

            db.MesaMembros.Remove(alvo);
        }

        // O token dele sai do mapa na mesma transação.
        IReadOnlyList<TokenRemovido> tokensRemovidos;
        await using (var transacao = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            tokensRemovidos = await tokensDeJogador.RemoverDoUsuarioAsync(mesaId, membroUsuarioId, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
        }

        logger.LogInformation(
            "Usuário {UsuarioId} {Motivo} da mesa {MesaId}",
            membroUsuarioId, saindoPorContaPropria ? "saiu" : "foi removido", mesaId);
        await notifier.MembroSaiuAsync(mesaId, membroUsuarioId, removidoPeloMestre: !saindoPorContaPropria);
        await tokensDeJogador.AvisarRemocaoAsync(tokensRemovidos);
        return Resultado.Ok;
    }

    private async Task<Resultado<MembroDto>> TrocarPersonagemAsync(MesaMembro membro, Guid personagemId, CancellationToken cancellationToken)
    {
        var personagem = await db.Personagens
            .FirstOrDefaultAsync(p => p.Id == personagemId && p.UsuarioId == membro.UsuarioId, cancellationToken);
        if (personagem is null)
        {
            return Erro.Invalido("personagemId", "Este personagem não pertence ao jogador.");
        }

        var mudou = membro.PersonagemAtivoId != personagem.Id;
        membro.PersonagemAtivoId = personagem.Id;
        membro.PersonagemAtivo = personagem;

        if (mudou)
        {
            await db.SaveChangesAsync(cancellationToken);
            await notifier.PersonagemAtivoAlteradoAsync(membro.MesaId, membro.UsuarioId, PersonagemResumoDto.ParaOLobby(personagem));
            await tokensDeJogador.GarantirAsync(membro.MesaId, membro.UsuarioId, cancellationToken);
        }

        var nomes = await usuarios.ObterNomesDeExibicaoAsync([membro.UsuarioId], cancellationToken);
        return MembroDto.De(membro, nomes.GetValueOrDefault(membro.UsuarioId, string.Empty));
    }

    private Task<MesaMembro?> ObterMembroAsync(Guid mesaId, Guid usuarioId, CancellationToken cancellationToken) =>
        db.MesaMembros
            .Include(membro => membro.PersonagemAtivo)
            .FirstOrDefaultAsync(membro => membro.MesaId == mesaId && membro.UsuarioId == usuarioId, cancellationToken);

    private async Task<(Mesa? Mesa, Erro? Erro)> ObterMesaComoMestreAsync(Guid usuarioId, Guid mesaId, CancellationToken cancellationToken)
    {
        var erro = await db.ExigirMestreAsync(usuarioId, mesaId, cancellationToken);
        if (erro is not null)
        {
            return (null, erro);
        }

        var mesa = await db.Mesas
            .Include(m => m.Membros).ThenInclude(membro => membro.PersonagemAtivo)
            .FirstAsync(m => m.Id == mesaId, cancellationToken);
        return (mesa, null);
    }

    private async Task<MesaDetalheDto> MontarDetalheAsync(Mesa mesa, Guid usuarioId, CancellationToken cancellationToken)
    {
        var nomes = await usuarios.ObterNomesDeExibicaoAsync(
            mesa.Membros.Select(membro => membro.UsuarioId).ToList(), cancellationToken);

        var membros = mesa.Membros
            .OrderBy(membro => membro.Papel)
            .ThenBy(membro => membro.EntrouEm)
            .Select(membro => MembroDto.De(membro, nomes.GetValueOrDefault(membro.UsuarioId, string.Empty)))
            .ToList();

        var meuPapel = mesa.Membros.First(membro => membro.UsuarioId == usuarioId).Papel;

        return new MesaDetalheDto(mesa.Id, mesa.Nome, mesa.Descricao, mesa.SessaoAtiva, meuPapel, mesa.CriadaEm, membros);
    }

    // O request já passou pelo MesaRequestValidator; aqui só normalizamos.
    private static void Aplicar(Mesa mesa, MesaRequest request)
    {
        mesa.Nome = request.Nome!.Trim();
        mesa.Descricao = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao.Trim();
    }
}
