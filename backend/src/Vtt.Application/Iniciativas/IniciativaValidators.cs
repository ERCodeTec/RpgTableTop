using FluentValidation;
using Vtt.Domain.Mapas;
using Vtt.Domain.Mesas;

namespace Vtt.Application.Iniciativas;

internal static class RegrasDeIniciativa
{
    public static readonly string ValorForaDoLimite =
        $"A iniciativa deve ficar entre {ValorDeIniciativa.ValorMinimo} e {ValorDeIniciativa.ValorMaximo}.";
}

public class ValorDeIniciativaRequestValidator : AbstractValidator<ValorDeIniciativaRequest>
{
    public ValorDeIniciativaRequestValidator()
    {
        RuleFor(x => x.Valor)
            .NotNull().WithMessage("Informe o valor da iniciativa.")
            .InclusiveBetween(ValorDeIniciativa.ValorMinimo, ValorDeIniciativa.ValorMaximo)
            .WithMessage(RegrasDeIniciativa.ValorForaDoLimite);
    }
}

public class ValorDeGrupoRequestValidator : AbstractValidator<ValorDeGrupoRequest>
{
    public ValorDeGrupoRequestValidator()
    {
        RuleFor(x => x.Grupo)
            .Must(grupo => !string.IsNullOrWhiteSpace(grupo)).WithMessage("Informe o grupo.")
            .Must(grupo => grupo is null || grupo.Trim().Length <= GrupoDeIniciativa.TamanhoMaximo)
            .WithMessage($"O grupo deve ter até {GrupoDeIniciativa.TamanhoMaximo} caracteres.");

        RuleFor(x => x.Valor)
            .NotNull().WithMessage("Informe o valor da iniciativa.")
            .InclusiveBetween(ValorDeIniciativa.ValorMinimo, ValorDeIniciativa.ValorMaximo)
            .WithMessage(RegrasDeIniciativa.ValorForaDoLimite);
    }
}

public class EncerrarVezRequestValidator : AbstractValidator<EncerrarVezRequest>
{
    public EncerrarVezRequestValidator()
    {
        RuleFor(x => x.TokenId).NotNull().WithMessage("Informe o token.");
    }
}
