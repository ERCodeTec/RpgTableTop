namespace Vtt.Api.Infrastructure;

public class AppOptions
{
    public const string Secao = "App";

    /// <summary>URL pública (ex.: a do ngrok), usada para montar links de convite a partir da Etapa 2.</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;
}

public class RateLimitingOptions
{
    public const string Secao = "RateLimiting";
    public const string PoliticaAuth = "auth";
    public const string PoliticaUpload = "upload";

    /// <summary>Requisições de login/cadastro permitidas por IP em cada janela.</summary>
    public int AuthLimite { get; set; } = 10;

    public int AuthJanelaSegundos { get; set; } = 60;

    /// <summary>Uploads de imagem permitidos por usuário a cada minuto; null é sem limite (o padrão).</summary>
    public int? UploadsPorMinuto { get; set; }

    /// <summary>Chamadas de MoverToken permitidas por conexão do Hub a cada segundo.</summary>
    public int MovimentosPorSegundo { get; set; } = 20;

    /// <summary>Chamadas dos demais métodos do Hub (entrar e sair da mesa) por conexão a cada segundo.</summary>
    public int ChamadasDoHubPorSegundo { get; set; } = 5;

    /// <summary>Maior mensagem que o Hub aceita de um cliente. As chamadas legítimas têm poucos bytes.</summary>
    public long TamanhoMaximoDaMensagemDoHub { get; set; } = 32 * 1024;
}
