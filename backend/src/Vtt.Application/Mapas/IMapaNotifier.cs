using Vtt.Domain.Mapas;

namespace Vtt.Application.Mapas;

/// <summary>
/// Quem recebe um evento do mapa. O mapa ativo é visto pela mesa inteira; um mapa em preparação
/// só interessa ao Mestre, e os jogadores não devem saber o que há nele.
/// </summary>
public readonly record struct AlcanceDoMapa(Guid MesaId, Guid? SomenteUsuarioId)
{
    public static AlcanceDoMapa MesaInteira(Guid mesaId) => new(mesaId, null);

    public static AlcanceDoMapa De(Mapa mapa, Guid mestreId) => new(mapa.MesaId, mapa.Ativo ? null : mestreId);
}

/// <summary>
/// Avisa em tempo real as mudanças no mapa. Os serviços chamam depois de gravar.
/// Nos eventos de token, cada público recebe a própria projeção (<see cref="ProjecoesDoToken"/>):
/// o Mestre vê tudo, o dono vê tudo do próprio token e os demais jogadores, só o que podem ver.
/// </summary>
public interface IMapaNotifier
{
    Task TokenCriadoAsync(AlcanceDoMapa alcance, ProjecoesDoToken token);

    /// <summary>
    /// Uma edição do token, inclusive de visibilidade: para os demais jogadores, um token que
    /// passou a ser visível chega como criado, e um que deixou de ser, como removido.
    /// </summary>
    Task TokenAlteradoAsync(AlcanceDoMapa alcance, ProjecoesDoToken antes, ProjecoesDoToken depois);

    Task TokenMovidoAsync(AlcanceDoMapa alcance, ProjecoesDoToken token);

    /// <summary>Movimento restante ou pendência mudou. Vai só ao Mestre e ao dono, que são quem os veem.</summary>
    Task MovimentoAlteradoAsync(AlcanceDoMapa alcance, ProjecoesDoToken token);

    /// <param name="token">As projeções de antes da remoção: dizem quem via o token.</param>
    Task TokenRemovidoAsync(AlcanceDoMapa alcance, ProjecoesDoToken token);

    /// <summary>Os demais jogadores só recebem o aviso se o HP que eles veem mudou.</summary>
    Task HpAlteradoAsync(AlcanceDoMapa alcance, ProjecoesDoToken antes, ProjecoesDoToken depois);

    /// <param name="estadoPara">
    /// Monta o estado do novo mapa ativo para um usuário (id e se é o Mestre); null quando a mesa
    /// ficou sem mapa ativo.
    /// </param>
    Task MapaAtivoAlteradoAsync(Guid mesaId, Func<Guid, bool, MapaEstadoDto>? estadoPara);

    Task MapaAtualizadoAsync(AlcanceDoMapa alcance, MapaDto mapa);

    Task EstruturaAdicionadaAsync(AlcanceDoMapa alcance, EstruturaDto estrutura);

    Task EstruturaAtualizadaAsync(AlcanceDoMapa alcance, EstruturaDto estrutura);

    Task EstruturaRemovidaAsync(AlcanceDoMapa alcance, Guid mapaId, Guid estruturaId);

    /// <summary>
    /// Camadas da névoa que mudaram. As comuns (do grupo e a revelada) vão à mesa inteira; a de um
    /// jogador, só a ele e ao Mestre.
    /// </summary>
    Task MemoriasDeNevoaAlteradasAsync(AlcanceDoMapa alcance, IReadOnlyList<MemoriaDeNevoaDto> memorias);
}
