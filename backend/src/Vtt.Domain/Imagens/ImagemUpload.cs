namespace Vtt.Domain.Imagens;

public class ImagemUpload
{
    public const int NomeOriginalTamanhoMaximo = 120;
    public const int LadoDaMiniatura = 256;

    public Guid Id { get; set; }

    /// <summary>A mesa dona da imagem. Nulo na imagem pessoal: exatamente um de MesaId e <see cref="UsuarioId"/> é preenchido.</summary>
    public Guid? MesaId { get; set; }

    /// <summary>O dono da imagem pessoal (galeria da biblioteca de tokens). Nulo nas imagens de mesa.</summary>
    public Guid? UsuarioId { get; set; }

    public Guid EnviadaPorUsuarioId { get; set; }

    /// <summary>
    /// Na cópia que uma imagem pessoal ganha ao entrar numa mesa, o id da original. Sem chave estrangeira:
    /// a original pode ser excluída e a cópia continua.
    /// </summary>
    public Guid? OrigemImagemId { get; set; }

    /// <summary>Nome que o arquivo tinha no computador do Mestre, sanitizado. Só para exibição: nunca entra no caminho.</summary>
    public string NomeOriginal { get; set; } = string.Empty;

    /// <summary>Definido pelo conteúdo do arquivo, não pelo que o cliente declarou.</summary>
    public string ContentType { get; set; } = string.Empty;

    public long TamanhoBytes { get; set; }
    public int Largura { get; set; }
    public int Altura { get; set; }

    /// <summary>Relativo à raiz de uploads, montado só com GUIDs gerados pelo servidor.</summary>
    public string CaminhoArmazenado { get; set; } = string.Empty;

    public DateTimeOffset CriadaEm { get; set; }

    public bool Pessoal => UsuarioId is not null;

    /// <summary>A miniatura da galeria fica ao lado do arquivo principal.</summary>
    public string CaminhoDaMiniatura => $"{CaminhoArmazenado[..CaminhoArmazenado.LastIndexOf('/')]}/{Id:N}.mini.webp";

    public static string CaminhoPara(Guid mesaId, Guid id, string extensao) => $"{PastaDaMesa(mesaId)}/{id:N}.{extensao}";

    public static string CaminhoPessoalPara(Guid usuarioId, Guid id, string extensao) => $"usuarios/{usuarioId:N}/{id:N}.{extensao}";

    public static string PastaDaMesa(Guid mesaId) => mesaId.ToString("N");
}
