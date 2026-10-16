namespace Vtt.Domain.Mesas;

/// <summary>Como os jogadores movem os próprios tokens no mapa. O Mestre move qualquer token livremente nos dois.</summary>
public enum ModoDeMovimentacao
{
    /// <summary>Clicando nas células do alcance, que gastam o movimento da rodada.</summary>
    PorBlocos,

    /// <summary>Arrastando para qualquer célula livre, sem custo de movimento.</summary>
    Livre
}
