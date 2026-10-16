namespace Vtt.Domain.Mapas;

public enum TipoEstrutura
{
    Parede,
    Porta,
    Pilar,
    Obstaculo,

    /// <summary>Piso desenhado rente ao chão, por baixo dos tokens. A altura não é usada no desenho.</summary>
    Chao
}
