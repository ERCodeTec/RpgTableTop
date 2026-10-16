namespace Vtt.Application.Mapas;

/// <summary>
/// Operações sobre as camadas de <see cref="Vtt.Domain.Mapas.MemoriaDeNevoa"/>: um bit por célula,
/// linha a linha, do bit menos significativo de cada byte para o mais. O mesmo formato de
/// <c>nevoa.ts</c> no cliente.
/// </summary>
internal static class MapaDeBits
{
    public static int Tamanho(int celulas) => (celulas + 7) / 8;

    public static byte[] Vazio(int celulas) => new byte[Tamanho(celulas)];

    /// <summary>
    /// Uma cópia de <paramref name="bits"/> no tamanho do mapa, sem os bits além da última célula,
    /// ou null se o tamanho não bate (o cliente mandou a camada de outro mapa ou de outras dimensões).
    /// </summary>
    public static byte[]? Normalizar(byte[]? bits, int celulas)
    {
        if (bits is null || bits.Length != Tamanho(celulas))
        {
            return null;
        }

        var copia = (byte[])bits.Clone();
        var sobra = celulas % 8;
        if (sobra != 0)
        {
            copia[^1] &= (byte)((1 << sobra) - 1);
        }

        return copia;
    }

    /// <summary>Acende em <paramref name="destino"/> os bits de <paramref name="origem"/>; true se algum era novo.</summary>
    public static bool Unir(byte[] destino, byte[] origem)
    {
        var mudou = false;
        for (var i = 0; i < destino.Length; i++)
        {
            var unido = (byte)(destino[i] | origem[i]);
            mudou |= unido != destino[i];
            destino[i] = unido;
        }

        return mudou;
    }

    /// <summary>Apaga em <paramref name="destino"/> os bits de <paramref name="mascara"/>; true se algum estava aceso.</summary>
    public static bool Apagar(byte[] destino, byte[] mascara)
    {
        var mudou = false;
        for (var i = 0; i < destino.Length; i++)
        {
            var restante = (byte)(destino[i] & ~mascara[i]);
            mudou |= restante != destino[i];
            destino[i] = restante;
        }

        return mudou;
    }

    /// <summary>A camada nas novas dimensões: cada célula fica onde estava, e as que saíram do mapa somem.</summary>
    public static byte[] Redimensionar(byte[] bits, int largura, int altura, int novaLargura, int novaAltura)
    {
        var novo = Vazio(novaLargura * novaAltura);
        for (var y = 0; y < Math.Min(altura, novaAltura); y++)
        {
            for (var x = 0; x < Math.Min(largura, novaLargura); x++)
            {
                if (Aceso(bits, y * largura + x))
                {
                    Acender(novo, y * novaLargura + x);
                }
            }
        }

        return novo;
    }

    public static bool Aceso(byte[] bits, int indice) =>
        indice >> 3 < bits.Length && (bits[indice >> 3] & (1 << (indice & 7))) != 0;

    public static void Acender(byte[] bits, int indice) => bits[indice >> 3] |= (byte)(1 << (indice & 7));
}
