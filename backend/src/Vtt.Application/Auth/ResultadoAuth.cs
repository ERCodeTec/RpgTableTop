namespace Vtt.Application.Auth;

/// <summary>
/// Resultado de cadastro/login. Em caso de falha, <see cref="Erros"/> traz as mensagens por campo
/// (vazio quando a falha é de credenciais, para não revelar qual dado está errado).
/// </summary>
public record ResultadoAuth(AuthResponse? Resposta, IReadOnlyDictionary<string, string[]> Erros)
{
    public bool Sucesso => Resposta is not null;

    public static ResultadoAuth Ok(AuthResponse resposta) =>
        new(resposta, new Dictionary<string, string[]>());

    public static ResultadoAuth Falha(IReadOnlyDictionary<string, string[]> erros) => new(null, erros);

    public static ResultadoAuth CredenciaisInvalidas() => new(null, new Dictionary<string, string[]>());
}
