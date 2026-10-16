using Vtt.Application.Common;

namespace Vtt.Application.Mapas;

/// <summary>
/// Criar, editar, duplicar, remover, alterar HP e exibição são do Mestre. O token que o servidor
/// cria para um jogador não é duplicado nem removido por aqui, e nele nome, ícone, cor e HP máximo
/// vêm do personagem.
/// </summary>
public interface ITokenService
{
    Task<Resultado<TokenDto>> CriarAsync(Guid usuarioId, Guid mapaId, CriarTokenRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cópias de um modelo da biblioteca do usuário, com HP cheio, nas primeiras células livres. Tudo ou nada:
    /// sem célula para todas, nenhuma é criada. Com imagem, o token usa a cópia dela na mesa.
    /// </summary>
    Task<Resultado<IReadOnlyList<TokenDto>>> CriarDaBibliotecaAsync(Guid usuarioId, Guid mapaId, CriarDaBibliotecaRequest request, CancellationToken cancellationToken = default);

    Task<Resultado<TokenDto>> EditarAsync(Guid usuarioId, Guid tokenId, EditarTokenRequest request, CancellationToken cancellationToken = default);

    /// <summary>Dano, cura ou edição direta do HP. O HP atual fica sempre entre 0 e o máximo; em 0 o token fica caído.</summary>
    Task<Resultado<TokenDto>> DefinirHpAsync(Guid usuarioId, Guid tokenId, HpRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Oculta ou revela o token e define o que os jogadores veem do HP. Um token revelado em uma
    /// célula onde já há outro visível vai para a célula livre mais próxima.
    /// </summary>
    Task<Resultado<TokenDto>> DefinirExibicaoAsync(Guid usuarioId, Guid tokenId, ExibicaoRequest request, CancellationToken cancellationToken = default);

    /// <summary>A cópia nasce na célula livre mais próxima do original.</summary>
    Task<Resultado<TokenDto>> DuplicarAsync(Guid usuarioId, Guid tokenId, CancellationToken cancellationToken = default);

    Task<Resultado> RemoverAsync(Guid usuarioId, Guid tokenId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Arraste livre, só do Mestre: qualquer token, para qualquer célula livre do mapa, sem custo
    /// de movimento (estruturas não barram) e descartando o movimento por blocos pendente.
    /// <paramref name="versaoEsperada"/> precisa ser a versão atual. Regra violada volta como
    /// movimento rejeitado; NaoEncontrado é para quem não pode saber que o token existe, o que
    /// inclui o jogador diante de um token oculto.
    /// </summary>
    Task<Resultado<MovimentoResultado>> MoverAsync(Guid usuarioId, Guid tokenId, int x, int y, int versaoEsperada, CancellationToken cancellationToken = default);

    /// <summary>
    /// Movimento por blocos: o dono move o próprio token, o Mestre qualquer um. O destino precisa
    /// estar ao alcance (<see cref="Alcance"/>) com o movimento restante; o custo do caminho mínimo
    /// é descontado e a origem fica guardada até concluir ou desfazer. Não começa com outro pendente.
    /// </summary>
    Task<Resultado<MovimentoResultado>> MoverPorBlocosAsync(Guid usuarioId, Guid tokenId, int x, int y, int versaoEsperada, CancellationToken cancellationToken = default);

    /// <summary>Volta o token à origem do movimento pendente e devolve o custo. A origem precisa estar livre.</summary>
    Task<Resultado<MovimentoResultado>> DesfazerMovimentoAsync(Guid usuarioId, Guid tokenId, int versaoEsperada, CancellationToken cancellationToken = default);

    /// <summary>Confirma o movimento pendente: o gasto fica e não dá mais para desfazer.</summary>
    Task<Resultado<MovimentoResultado>> ConcluirMovimentoAsync(Guid usuarioId, Guid tokenId, int versaoEsperada, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disparada: o dono ou o Mestre soma mais um deslocamento inteiro ao movimento restante, o
    /// que dobra o movimento da rodada. Uma vez por rodada; vale também com movimento pendente.
    /// </summary>
    Task<Resultado<MovimentoResultado>> DispararAsync(Guid usuarioId, Guid tokenId, int versaoEsperada, CancellationToken cancellationToken = default);

    /// <summary>
    /// Volta atrás na Disparada da rodada, tirando o deslocamento que ela somou. Só enquanto esse
    /// extra não foi gasto: com um movimento pendente que o usou, é preciso desfazê-lo antes.
    /// </summary>
    Task<Resultado<MovimentoResultado>> DesfazerDisparadaAsync(Guid usuarioId, Guid tokenId, int versaoEsperada, CancellationToken cancellationToken = default);

    /// <summary>
    /// Só o Mestre: devolve o movimento inteiro (e a Disparada) a todos os tokens do mapa e descarta as pendências.
    /// No mapa ativo durante o combate, é Conflito: a rodada vira pela iniciativa.
    /// </summary>
    Task<Resultado> NovaRodadaAsync(Guid usuarioId, Guid mapaId, CancellationToken cancellationToken = default);

    /// <summary>Só o Mestre: o mesmo da nova rodada, para um token.</summary>
    Task<Resultado<TokenDto>> RestaurarMovimentoAsync(Guid usuarioId, Guid tokenId, CancellationToken cancellationToken = default);
}
