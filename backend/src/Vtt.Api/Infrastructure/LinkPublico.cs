using Microsoft.Extensions.Options;
using Vtt.Application.Common;

namespace Vtt.Api.Infrastructure;

/// <summary>
/// Usa App:PublicBaseUrl (PUBLIC_BASE_URL). Sem ela, cai no host da própria requisição,
/// que atrás do ngrok já vem corrigido pelos cabeçalhos X-Forwarded-*.
/// </summary>
public class LinkPublico(IOptions<AppOptions> options, IHttpContextAccessor httpContextAccessor) : ILinkPublico
{
    public string Convite(string codigo) => $"{BaseUrl()}/convite/{codigo}";

    private string BaseUrl()
    {
        var configurada = options.Value.PublicBaseUrl;
        if (!string.IsNullOrWhiteSpace(configurada))
        {
            return configurada.TrimEnd('/');
        }

        var request = httpContextAccessor.HttpContext?.Request;
        return request is null ? string.Empty : $"{request.Scheme}://{request.Host}";
    }
}
