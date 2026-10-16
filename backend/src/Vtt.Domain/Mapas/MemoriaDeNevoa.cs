namespace Vtt.Domain.Mapas;

public enum TipoDeMemoria
{
    /// <summary>O que os tokens já viram: aparece escurecido, sem os tokens que estão lá agora.</summary>
    Explorada,

    /// <summary>O que o Mestre revelou à mão: aparece para todos como se estivesse à vista.</summary>
    Revelada
}

/// <summary>
/// Uma camada de células do mapa, em bits: a célula (x, y) é o bit <c>y * largura + x</c>, do
/// menos para o mais significativo de cada byte.
/// </summary>
public class MemoriaDeNevoa
{
    public Guid Id { get; set; }
    public Guid MapaId { get; set; }
    public TipoDeMemoria Tipo { get; set; }

    /// <summary>
    /// Nulo na memória do grupo (névoa compartilhada) e na camada revelada pelo Mestre; preenchido
    /// na memória de um jogador (névoa individual).
    /// </summary>
    public Guid? UsuarioId { get; set; }

    public byte[] Celulas { get; set; } = [];

    public int Versao { get; set; } = 1;
}
