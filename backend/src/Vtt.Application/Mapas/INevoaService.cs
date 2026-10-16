using Vtt.Application.Common;

namespace Vtt.Application.Mapas;

/// <summary>
/// A névoa de guerra de um mapa. O cliente calcula o que cada um vê; aqui ficam o modo, escolhido
/// pelo Mestre, e a memória do que já foi visto ou revelado, gravada e repassada a quem a vê.
/// </summary>
public interface INevoaService
{
    /// <summary>Só o Mestre: liga, desliga ou troca o modo da névoa do mapa.</summary>
    Task<Resultado<MapaDto>> DefinirModoAsync(Guid usuarioId, Guid mapaId, NevoaRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Um jogador informa as células que os tokens dele veem agora. Elas entram na memória do grupo
    /// (névoa compartilhada) ou na dele (individual). Só vale no mapa ativo com a névoa ligada; uma
    /// camada que não acrescenta nada não gera aviso.
    /// </summary>
    Task<Resultado> ExplorarAsync(Guid usuarioId, Guid mapaId, byte[]? celulas, CancellationToken cancellationToken = default);

    /// <summary>Só o Mestre: o pincel. Revelar acende as células para todos; cobrir as apaga, também do que foi explorado.</summary>
    Task<Resultado> RevelarAsync(Guid usuarioId, Guid mapaId, RevelarCelulasRequest request, CancellationToken cancellationToken = default);

    /// <summary>Só o Mestre: esquece o que foi explorado (do grupo e de cada jogador). O que ele revelou à mão fica.</summary>
    Task<Resultado> LimparMemoriaAsync(Guid usuarioId, Guid mapaId, CancellationToken cancellationToken = default);
}
