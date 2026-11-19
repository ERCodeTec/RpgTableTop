using Vtt.Application.Common;

namespace Vtt.Application.Mapas;

/// <summary>
/// Pastas da aba Mapas. Tudo é do Mestre: quem não é membro recebe NaoEncontrado e o jogador,
/// Proibido. Uma pasta de outra mesa se comporta como inexistente.
/// </summary>
public interface IPastaDeMapasService
{
    /// <summary>Em ordem alfabética, com a quantidade de mapas de cada uma.</summary>
    Task<Resultado<IReadOnlyList<PastaDeMapasDto>>> ListarAsync(Guid usuarioId, Guid mesaId, CancellationToken cancellationToken = default);

    /// <summary>Conflito se o nome repetir na mesa (sem diferenciar maiúsculas) ou o limite de pastas for atingido.</summary>
    Task<Resultado<PastaDeMapasDto>> CriarAsync(Guid usuarioId, Guid mesaId, PastaDeMapasRequest request, CancellationToken cancellationToken = default);

    /// <summary>Troca o nome e a cor; sem cor no pedido, volta à padrão.</summary>
    Task<Resultado<PastaDeMapasDto>> RenomearAsync(Guid usuarioId, Guid id, PastaDeMapasRequest request, CancellationToken cancellationToken = default);

    /// <summary>Os mapas da pasta ficam em "Sem pasta".</summary>
    Task<Resultado> ExcluirAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Põe o mapa em uma pasta da mesma mesa ou, com PastaId nulo, tira de qualquer pasta.</summary>
    Task<Resultado<MapaDto>> DefinirPastaDoMapaAsync(Guid usuarioId, Guid mapaId, PastaDoMapaRequest request, CancellationToken cancellationToken = default);
}
