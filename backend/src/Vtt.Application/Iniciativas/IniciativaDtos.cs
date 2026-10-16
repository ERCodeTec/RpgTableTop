using Vtt.Domain.Mesas;

namespace Vtt.Application.Iniciativas;

/// <param name="Grupo">A chave do grupo (<see cref="Domain.Mapas.GrupoDeIniciativa"/>); nulo no valor de um jogador.</param>
public record ValorDeIniciativaDto(Guid? UsuarioId, string? Grupo, int Valor);

/// <summary>
/// A iniciativa da mesa. A ordem não vem pronta: cada cliente a monta com os tokens que vê e a regra de
/// <see cref="OrdemDeIniciativa"/>, então os ocultos nunca entram na trilha dos jogadores.
/// </summary>
/// <param name="Valores">Para os jogadores, sem os grupos que não têm token visível no mapa ativo.</param>
/// <param name="JogaramNaRodada">Os tokens do mapa ativo cuja vez já passou nesta rodada.</param>
public record IniciativaDto(
    EstadoDaIniciativa Estado,
    int Rodada,
    Guid? VezDoTokenId,
    IReadOnlyList<ValorDeIniciativaDto> Valores,
    IReadOnlyList<Guid> JogaramNaRodada);

public record ValorDeIniciativaRequest(int? Valor);

/// <param name="Grupo">O nome do grupo, como aparece nos tokens ("Goblin"); um número final é ignorado.</param>
public record ValorDeGrupoRequest(string? Grupo, int? Valor);

public record EncerrarVezRequest(Guid? TokenId);
