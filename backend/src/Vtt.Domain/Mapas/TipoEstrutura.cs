namespace Vtt.Domain.Mapas;

public enum TipoEstrutura
{
    Parede,
    Porta,
    Pilar,
    Obstaculo,

    /// <summary>Piso desenhado rente ao chão, por baixo dos tokens. A altura não é usada no desenho.</summary>
    Chao,

    /// <summary>
    /// Área onde os tokens dos jogadores nascem ao entrar no mapa. Nunca bloqueia e só o Mestre a
    /// vê, no modo de edição. Sem nenhuma no mapa, o spawn varre a partir de (0,0).
    /// </summary>
    Entrada
}
