using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vtt.Application.Common;
using Vtt.Domain.Mapas;
using Vtt.Domain.Mesas;
using Vtt.Domain.Personagens;

namespace Vtt.Application.Mapas;

/// <summary>Um token de jogador que deixou de existir, para avisar a mesa depois de gravar.</summary>
public readonly record struct TokenRemovido(Guid MesaId, ProjecoesDoToken Token);

/// <summary>
/// A regra do token que o servidor mantém para cada jogador: um por jogador, sempre no mapa ativo
/// da mesa, espelhando o personagem ativo dele.
/// As alterações usam UPDATE/DELETE diretos, que não disputam a versão com um MoverToken em andamento.
/// O HP atual do token é da cena: ele não volta para o cadastro do personagem, que guarda só o máximo.
/// </summary>
public class TokensDeJogador(IAppDbContext db, IMapaNotifier notifier, ILogger<TokensDeJogador> logger)
{
    private const int TentativasDeCriacao = 3;

    /// <summary>
    /// Deixa o token do jogador de acordo com o personagem ativo dele no mapa ativo: cria no ponto
    /// de spawn se não existir, ou atualiza mantendo a posição. Trocar de personagem restaura o HP.
    /// Chamado depois de gravar a entrada na mesa ou a troca de personagem.
    /// </summary>
    public async Task GarantirAsync(Guid mesaId, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var membro = await db.MesaMembros
            .AsNoTracking()
            .Include(m => m.PersonagemAtivo)
            .FirstOrDefaultAsync(m => m.MesaId == mesaId && m.UsuarioId == usuarioId, cancellationToken);
        if (membro is not { Papel: PapelNaMesa.Jogador, PersonagemAtivo: { } personagem })
        {
            return;
        }

        var mapa = await db.Mapas
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MesaId == mesaId && m.Ativo, cancellationToken);
        if (mapa is null)
        {
            return;
        }

        var alcance = AlcanceDoMapa.MesaInteira(mesaId);
        for (var tentativa = 0; tentativa < TentativasDeCriacao; tentativa++)
        {
            var existente = await db.Tokens
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.MapaId == mapa.Id && t.DonoUsuarioId == usuarioId, cancellationToken);
            if (existente is not null)
            {
                var trocouDePersonagem = existente.PersonagemId != personagem.Id;
                await EspelharAsync(db.Tokens.Where(t => t.Id == existente.Id), personagem, trocouDePersonagem, cancellationToken);

                var atualizado = await db.Tokens.AsNoTracking().FirstOrDefaultAsync(t => t.Id == existente.Id, cancellationToken);
                if (atualizado is not null)
                {
                    await notifier.TokenAlteradoAsync(alcance, ProjecoesDoToken.De(existente), ProjecoesDoToken.De(atualizado));
                }

                return;
            }

            // Tokens ocultos não contam: a posição do spawn não pode denunciar onde eles estão.
            var ocupadas = await db.CelulasIndisponiveisAsync(mapa.Id, somenteVisiveis: true, cancellationToken);
            var entradas = await db.EntradasAsync(mapa.Id, cancellationToken);
            if (Grade.PontoDeSpawn(mapa.Largura, mapa.Altura, entradas, ocupadas) is not { } celula)
            {
                logger.LogWarning("Mapa {MapaId} cheio: o token do jogador {UsuarioId} não foi criado", mapa.Id, usuarioId);
                return;
            }

            var novo = Novo(mapa.Id, usuarioId, personagem, celula);
            db.Tokens.Add(novo);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Outra gravação ocupou a célula (ou criou o token dele) ao mesmo tempo: tenta de novo.
                db.Tokens.Entry(novo).State = EntityState.Detached;
                continue;
            }

            await notifier.TokenCriadoAsync(alcance, ProjecoesDoToken.De(novo));
            return;
        }

        logger.LogWarning("Não foi possível criar o token do jogador {UsuarioId} no mapa {MapaId}", usuarioId, mapa.Id);
    }

    /// <summary>Reflete nos tokens a edição do personagem. Chamado depois de gravá-la.</summary>
    public async Task SincronizarPersonagemAsync(Personagem personagem, CancellationToken cancellationToken = default)
    {
        var antes = await db.Tokens
            .AsNoTracking()
            .Where(t => t.PersonagemId == personagem.Id)
            .ToDictionaryAsync(t => t.Id, cancellationToken);
        if (antes.Count == 0)
        {
            return;
        }

        await EspelharAsync(db.Tokens.Where(t => t.PersonagemId == personagem.Id), personagem, reiniciarHp: false, cancellationToken);

        var depois = await db.Tokens
            .AsNoTracking()
            .Where(t => t.PersonagemId == personagem.Id)
            .Join(db.Mapas, t => t.MapaId, m => m.Id, (t, m) => new { Token = t, m.MesaId })
            .ToListAsync(cancellationToken);
        foreach (var item in depois)
        {
            if (antes.TryGetValue(item.Token.Id, out var anterior))
            {
                await notifier.TokenAlteradoAsync(
                    AlcanceDoMapa.MesaInteira(item.MesaId), ProjecoesDoToken.De(anterior), ProjecoesDoToken.De(item.Token));
            }
        }
    }

    /// <summary>
    /// Apaga o token do jogador na mesa (ele saiu ou foi removido). Quem chama grava a saída na
    /// mesma transação e depois avisa com <see cref="AvisarRemocaoAsync"/>.
    /// </summary>
    public Task<IReadOnlyList<TokenRemovido>> RemoverDoUsuarioAsync(Guid mesaId, Guid usuarioId, CancellationToken cancellationToken = default) =>
        RemoverAsync(
            db.Tokens.Where(t => t.DonoUsuarioId == usuarioId && db.Mapas.Any(m => m.Id == t.MapaId && m.MesaId == mesaId)),
            cancellationToken);

    /// <summary>Apaga os tokens do personagem que está sendo excluído, nas mesmas condições de <see cref="RemoverDoUsuarioAsync"/>.</summary>
    public Task<IReadOnlyList<TokenRemovido>> RemoverDoPersonagemAsync(Guid personagemId, CancellationToken cancellationToken = default) =>
        RemoverAsync(db.Tokens.Where(t => t.PersonagemId == personagemId), cancellationToken);

    public async Task AvisarRemocaoAsync(IReadOnlyList<TokenRemovido> removidos)
    {
        foreach (var removido in removidos)
        {
            await notifier.TokenRemovidoAsync(AlcanceDoMapa.MesaInteira(removido.MesaId), removido.Token);
        }
    }

    /// <summary>
    /// Na ativação de um mapa: move para ele os tokens dos jogadores, no ponto de spawn, e cria os
    /// que faltam. Roda dentro da transação da ativação e não avisa ninguém; o estado completo do
    /// novo mapa segue no evento de troca.
    /// </summary>
    public async Task TrazerParaOMapaAsync(Mapa mapa, CancellationToken cancellationToken = default)
    {
        var jogadores = await db.MesaMembros
            .AsNoTracking()
            .Include(m => m.PersonagemAtivo)
            .Where(m => m.MesaId == mapa.MesaId && m.Papel == PapelNaMesa.Jogador && m.PersonagemAtivoId != null)
            .OrderBy(m => m.EntrouEm)
            .ToListAsync(cancellationToken);

        var existentes = await db.Tokens
            .AsNoTracking()
            .Where(t => t.DonoUsuarioId != null && db.Mapas.Any(m => m.Id == t.MapaId && m.MesaId == mapa.MesaId))
            .ToListAsync(cancellationToken);

        var ocupadas = await db.CelulasIndisponiveisAsync(mapa.Id, somenteVisiveis: true, cancellationToken);
        var entradas = await db.EntradasAsync(mapa.Id, cancellationToken);
        foreach (var jogador in jogadores)
        {
            var token = existentes.FirstOrDefault(t => t.DonoUsuarioId == jogador.UsuarioId);
            if (token?.MapaId == mapa.Id)
            {
                continue;
            }

            if (Grade.PontoDeSpawn(mapa.Largura, mapa.Altura, entradas, ocupadas) is not { } celula)
            {
                logger.LogWarning("Mapa {MapaId} cheio: o token do jogador {UsuarioId} ficou de fora", mapa.Id, jogador.UsuarioId);
                continue;
            }

            ocupadas.Add(celula);
            if (token is null)
            {
                db.Tokens.Add(Novo(mapa.Id, jogador.UsuarioId, jogador.PersonagemAtivo!, celula));
            }
            else
            {
                await db.Tokens
                    .Where(t => t.Id == token.Id)
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(t => t.MapaId, mapa.Id)
                            .SetProperty(t => t.X, celula.X)
                            .SetProperty(t => t.Y, celula.Y)
                            // Mapa novo, rodada nova: o movimento volta inteiro e a pendência some.
                            .SetProperty(t => t.MovimentoRestante, t => t.Deslocamento)
                            .SetProperty(t => t.Disparada, false)
                            .SetProperty(t => t.OrigemPendenteX, (int?)null)
                            .SetProperty(t => t.OrigemPendenteY, (int?)null)
                            .SetProperty(t => t.CustoPendente, 0)
                            .SetProperty(t => t.Versao, t => t.Versao + 1),
                        cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static Token Novo(Guid mapaId, Guid usuarioId, Personagem personagem, (int X, int Y) celula) => new()
    {
        Id = Guid.NewGuid(),
        MapaId = mapaId,
        Tipo = TipoToken.Personagem,
        Nome = personagem.Nome,
        HpAtual = personagem.HpMaximo,
        HpMaximo = personagem.HpMaximo,
        X = celula.X,
        Y = celula.Y,
        CorBorda = personagem.CorBorda,
        Icone = personagem.Classe.ToString(),
        PersonagemId = personagem.Id,
        ImagemId = personagem.ImagemId,
        DonoUsuarioId = usuarioId,
        ExibicaoHp = Token.ExibicaoHpPadrao(TipoToken.Personagem),
        Deslocamento = personagem.Deslocamento,
        MovimentoRestante = personagem.Deslocamento,
        Visao = personagem.Visao
    };

    /// <summary>
    /// Copia os dados do personagem para os tokens; o HP atual nunca passa do novo máximo. Uma
    /// mudança de deslocamento mantém o que já foi gasto na rodada (como <see cref="Token.DefinirDeslocamento"/>);
    /// trocar de personagem restaura o movimento.
    /// </summary>
    private static Task<int> EspelharAsync(IQueryable<Token> tokens, Personagem personagem, bool reiniciarHp, CancellationToken cancellationToken)
    {
        var icone = personagem.Classe.ToString();
        var hpMaximo = personagem.HpMaximo;
        var deslocamento = personagem.Deslocamento;
        var visao = personagem.Visao;
        var imagemId = personagem.ImagemId;
        return tokens.ExecuteUpdateAsync(
            s => s.SetProperty(t => t.Nome, personagem.Nome)
                .SetProperty(t => t.Icone, icone)
                .SetProperty(t => t.CorBorda, personagem.CorBorda)
                .SetProperty(t => t.PersonagemId, personagem.Id)
                .SetProperty(t => t.ImagemId, imagemId)
                .SetProperty(t => t.HpMaximo, hpMaximo)
                .SetProperty(t => t.HpAtual, t => reiniciarHp || t.HpAtual > hpMaximo ? hpMaximo : t.HpAtual)
                // Com a Disparada usada, o máximo é o dobro, e a diferença do deslocamento conta em dobro.
                .SetProperty(
                    t => t.MovimentoRestante,
                    t => reiniciarHp
                        ? deslocamento
                        : Math.Max(0, Math.Min(
                            t.Disparada ? deslocamento * 2 : deslocamento,
                            t.MovimentoRestante + (t.Disparada ? 2 : 1) * (deslocamento - t.Deslocamento))))
                .SetProperty(t => t.Disparada, t => !reiniciarHp && t.Disparada)
                .SetProperty(t => t.Deslocamento, deslocamento)
                .SetProperty(t => t.Visao, visao)
                .SetProperty(t => t.Versao, t => t.Versao + 1),
            cancellationToken);
    }

    private async Task<IReadOnlyList<TokenRemovido>> RemoverAsync(IQueryable<Token> tokens, CancellationToken cancellationToken)
    {
        var encontrados = await tokens
            .AsNoTracking()
            .Join(db.Mapas, t => t.MapaId, m => m.Id, (t, m) => new { Token = t, m.MesaId })
            .ToListAsync(cancellationToken);
        if (encontrados.Count > 0)
        {
            var ids = encontrados.Select(encontrado => encontrado.Token.Id).ToList();
            await db.Tokens.Where(t => ids.Contains(t.Id)).ExecuteDeleteAsync(cancellationToken);
        }

        return encontrados
            .Select(encontrado => new TokenRemovido(encontrado.MesaId, ProjecoesDoToken.De(encontrado.Token)))
            .ToList();
    }
}
