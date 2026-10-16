namespace Vtt.Domain.Mesas;

/// <summary>Em que ponto está a iniciativa da mesa. Vale para todos os mapas que o Mestre ativar.</summary>
public enum EstadoDaIniciativa
{
    /// <summary>Sem iniciativa: os tokens se movem sem ordem.</summary>
    Inativa,

    /// <summary>O Mestre pediu a iniciativa e os valores estão chegando; ainda não há vez.</summary>
    Coletando,

    /// <summary>A ordem está valendo: com a movimentação por blocos, só o token da vez anda.</summary>
    EmCombate
}
