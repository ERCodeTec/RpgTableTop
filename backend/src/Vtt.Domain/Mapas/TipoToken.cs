namespace Vtt.Domain.Mapas;

public enum TipoToken
{
    Personagem,
    Inimigo,
    Npc,

    /// <summary>Um objeto do cenário (baú, carta, estátua) cujo conteúdo os jogadores examinam de perto.</summary>
    Interacao
}
