using Vtt.Application.Common;

namespace Vtt.Application.Iniciativas;

/// <summary>
/// A iniciativa da mesa. O Mestre pede, os jogadores informam o próprio valor uma vez e o Mestre
/// define o dos grupos de inimigos e NPCs. Os valores valem em todos os mapas até o próximo pedido.
/// As respostas trazem a iniciativa como quem pediu a vê; a mesa recebe IniciativaAlterada.
/// </summary>
public interface IIniciativaService
{
    Task<Resultado<IniciativaDto>> ObterAsync(Guid usuarioId, Guid mesaId, CancellationToken cancellationToken = default);

    /// <summary>Só o Mestre: apaga os valores anteriores e passa a coletar os novos. Um combate em andamento para.</summary>
    Task<Resultado<IniciativaDto>> SolicitarAsync(Guid usuarioId, Guid mesaId, CancellationToken cancellationToken = default);

    /// <summary>O jogador informa a própria iniciativa, uma vez por pedido. Vale também com o combate já iniciado.</summary>
    Task<Resultado<IniciativaDto>> InformarMinhaAsync(Guid usuarioId, Guid mesaId, int valor, CancellationToken cancellationToken = default);

    /// <summary>Só o Mestre: define ou corrige a iniciativa de um jogador, desde que ele não tenha jogado na rodada.</summary>
    Task<Resultado<IniciativaDto>> DefinirDoJogadorAsync(Guid usuarioId, Guid mesaId, Guid jogadorId, int valor, CancellationToken cancellationToken = default);

    /// <summary>Só o Mestre: a iniciativa de um grupo de tokens iguais, desde que nenhum deles tenha jogado na rodada.</summary>
    Task<Resultado<IniciativaDto>> DefinirDoGrupoAsync(Guid usuarioId, Guid mesaId, string grupo, int valor, CancellationToken cancellationToken = default);

    /// <summary>Só o Mestre: começa a rodada 1, com o movimento inteiro, na vez do primeiro da ordem.</summary>
    Task<Resultado<IniciativaDto>> IniciarAsync(Guid usuarioId, Guid mesaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// O dono do token da vez, ou o Mestre (que assim pula a vez de qualquer um), passa a vez ao
    /// próximo. O movimento pendente fica concluído.
    /// </summary>
    Task<Resultado<IniciativaDto>> EncerrarVezAsync(Guid usuarioId, Guid mesaId, Guid tokenId, CancellationToken cancellationToken = default);

    /// <summary>Só o Mestre: a iniciativa volta a inativa e os valores são apagados.</summary>
    Task<Resultado<IniciativaDto>> EncerrarCombateAsync(Guid usuarioId, Guid mesaId, CancellationToken cancellationToken = default);
}
