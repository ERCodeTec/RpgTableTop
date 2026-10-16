using Microsoft.AspNetCore.Mvc;
using Vtt.Application.Common;

namespace Vtt.Api.Infrastructure;

public static class ResultadoExtensions
{
    /// <summary>Traduz um <see cref="Erro"/> da camada Application para a resposta ProblemDetails equivalente.</summary>
    public static IActionResult Problema(this ControllerBase controller, Erro erro)
    {
        if (erro.Tipo == TipoErro.Invalido)
        {
            controller.ModelState.AddModelError(erro.Campo ?? string.Empty, erro.Mensagem);
            return controller.ValidationProblem(controller.ModelState);
        }

        if (erro.Tipo == TipoErro.Proibido)
        {
            // Um membro tentou uma ação que o papel dele não permite.
            controller.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Vtt.Autorizacao")
                .LogWarning(
                    "Acesso negado: usuário {UsuarioId} em {Metodo} {Caminho}",
                    controller.User.ObterUsuarioId(), controller.Request.Method, controller.Request.Path.Value);
        }

        var status = erro.Tipo switch
        {
            TipoErro.NaoEncontrado => StatusCodes.Status404NotFound,
            TipoErro.Proibido => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status409Conflict
        };

        return controller.Problem(statusCode: status, detail: erro.Mensagem);
    }
}
