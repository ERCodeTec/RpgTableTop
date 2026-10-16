namespace Vtt.Domain.Mapas;

/// <summary>O que os jogadores (fora o dono do token) veem do HP. O Mestre e o dono sempre veem tudo.</summary>
public enum ExibicaoHp
{
    /// <summary>Nem barra nem números.</summary>
    Oculto,

    /// <summary>Só a barra proporcional, sem valores.</summary>
    Barra,

    /// <summary>Barra e números.</summary>
    Completo
}
