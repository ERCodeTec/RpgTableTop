using Vtt.Application.Common;
using Vtt.Domain.Mesas;

namespace Vtt.Application.Mapas;

/// <summary>
/// Como nas mesas: quem não é membro recebe NaoEncontrado e o jogador recebe Proibido nas
/// operações do Mestre. Para o jogador, um mapa que não é o ativo também é NaoEncontrado.
/// </summary>
public interface IMapaService
{
    Task<Resultado<MapaDto>> CriarAsync(Guid usuarioId, Guid mesaId, MapaRequest request, CancellationToken cancellationToken = default);

    /// <summary>Cria um mapa inativo com uma masmorra gerada (salas, corredores, portas), tudo de uma vez.</summary>
    Task<Resultado<MapaEstadoDto>> GerarAsync(Guid usuarioId, Guid mesaId, MapaProceduralRequest request, CancellationToken cancellationToken = default);

    /// <summary>O Mestre vê todos os mapas; o jogador, só o ativo.</summary>
    Task<Resultado<IReadOnlyList<MapaDto>>> ListarAsync(Guid usuarioId, Guid mesaId, CancellationToken cancellationToken = default);

    /// <summary>Reduzir as dimensões é Conflito enquanto houver token ou estrutura fora dos novos limites.</summary>
    Task<Resultado<MapaDto>> AtualizarAsync(Guid usuarioId, Guid mapaId, MapaRequest request, CancellationToken cancellationToken = default);

    /// <summary>Define (ou remove) a imagem de fundo e o ajuste dela ao grid.</summary>
    Task<Resultado<MapaDto>> DefinirFundoAsync(Guid usuarioId, Guid mapaId, FundoRequest request, CancellationToken cancellationToken = default);

    Task<Resultado> ExcluirAsync(Guid usuarioId, Guid mapaId, CancellationToken cancellationToken = default);

    /// <summary>Torna o mapa o ativo da mesa, desativa o anterior e traz os tokens dos jogadores.</summary>
    Task<Resultado<MapaEstadoDto>> AtivarAsync(Guid usuarioId, Guid mapaId, CancellationToken cancellationToken = default);

    Task<Resultado<MapaEstadoDto>> ObterEstadoAsync(Guid usuarioId, Guid mapaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Estado do mapa ativo da mesa como o usuário o vê, sem checar acesso: quem chama já sabe que
    /// ele é membro e com qual papel.
    /// </summary>
    Task<MapaEstadoDto?> ObterEstadoAtivoAsync(Guid mesaId, Guid usuarioId, PapelNaMesa papel, CancellationToken cancellationToken = default);
}
