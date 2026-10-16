namespace Vtt.Domain.Mapas;

/// <summary>
/// A névoa de guerra de um mapa. É visual: o cálculo do que cada um vê fica no cliente, e o
/// servidor só guarda a memória do que já foi explorado (<see cref="MemoriaDeNevoa"/>).
/// </summary>
public enum ModoDeNevoa
{
    /// <summary>Os jogadores veem o mapa inteiro.</summary>
    Desligada,

    /// <summary>Cada jogador vê o que qualquer token do grupo vê, e a memória é uma só.</summary>
    Compartilhada,

    /// <summary>Cada jogador vê pelo próprio token (e pelos aliados), com a própria memória.</summary>
    Individual
}
