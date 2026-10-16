using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Vtt.Api.Infrastructure;

/// <summary>
/// Roda o validador do FluentValidation registrado para cada argumento da action
/// e responde 400 com ValidationProblemDetails quando há erros.
/// </summary>
public class ValidationFilter(IServiceProvider services, ProblemDetailsFactory problemDetailsFactory) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argumento in context.ActionArguments.Values)
        {
            if (argumento is null)
            {
                continue;
            }

            var tipoValidador = typeof(IValidator<>).MakeGenericType(argumento.GetType());
            if (services.GetService(tipoValidador) is not IValidator validador)
            {
                continue;
            }

            var resultado = await validador.ValidateAsync(
                new ValidationContext<object>(argumento), context.HttpContext.RequestAborted);

            foreach (var erro in resultado.Errors)
            {
                context.ModelState.AddModelError(JsonNamingPolicy.CamelCase.ConvertName(erro.PropertyName), erro.ErrorMessage);
            }
        }

        if (!context.ModelState.IsValid)
        {
            var problema = problemDetailsFactory.CreateValidationProblemDetails(context.HttpContext, context.ModelState);
            context.Result = new BadRequestObjectResult(problema);
            return;
        }

        await next();
    }
}
