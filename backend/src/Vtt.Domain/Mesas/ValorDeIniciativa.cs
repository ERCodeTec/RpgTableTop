namespace Vtt.Domain.Mesas;

/// <summary>
/// A iniciativa de um jogador ou de um grupo de tokens (inimigos e NPCs de mesmo nome), na mesa.
/// Fica até o Mestre pedir a iniciativa de novo ou encerrar o combate.
/// </summary>
public class ValorDeIniciativa
{
    public const int ValorMinimo = -10;
    public const int ValorMaximo = 99;

    public Guid Id { get; set; }
    public Guid MesaId { get; set; }

    /// <summary>Preenchido na iniciativa de um jogador; vale para o token dele em qualquer mapa.</summary>
    public Guid? UsuarioId { get; set; }

    /// <summary>Preenchido na iniciativa de um grupo: a chave de <see cref="Mapas.GrupoDeIniciativa"/>.</summary>
    public string? Grupo { get; set; }

    public int Valor { get; set; }
    public DateTimeOffset InformadoEm { get; set; }
}
