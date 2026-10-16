using FluentValidation;
using Vtt.Domain.Mesas;

namespace Vtt.Application.Mesas;

public class MesaRequestValidator : AbstractValidator<MesaRequest>
{
    public MesaRequestValidator()
    {
        RuleFor(x => x.Nome)
            .Must(nome => !string.IsNullOrWhiteSpace(nome)).WithMessage("Informe o nome da mesa.")
            .Must(nome => nome is null || nome.Trim().Length <= Mesa.NomeTamanhoMaximo)
            .WithMessage($"O nome deve ter até {Mesa.NomeTamanhoMaximo} caracteres.");

        RuleFor(x => x.Descricao)
            .Must(descricao => descricao is null || descricao.Trim().Length <= Mesa.DescricaoTamanhoMaximo)
            .WithMessage($"A descrição deve ter até {Mesa.DescricaoTamanhoMaximo} caracteres.");
    }
}

public class ConviteRequestValidator : AbstractValidator<ConviteRequest>
{
    public const int ValidadeHorasMaxima = 24 * 365;
    public const int UsosMaximosLimite = 1000;

    public ConviteRequestValidator()
    {
        RuleFor(x => x.ValidadeHoras)
            .InclusiveBetween(1, ValidadeHorasMaxima)
            .WithMessage($"A validade deve ficar entre 1 e {ValidadeHorasMaxima} horas.");

        RuleFor(x => x.UsosMaximos)
            .InclusiveBetween(1, UsosMaximosLimite)
            .WithMessage($"O limite de usos deve ficar entre 1 e {UsosMaximosLimite}.");
    }
}

public class DefinirPersonagemRequestValidator : AbstractValidator<DefinirPersonagemRequest>
{
    public DefinirPersonagemRequestValidator()
    {
        RuleFor(x => x.PersonagemId).NotEmpty().WithMessage("Escolha um personagem.");
    }
}

public class AceitarConviteRequestValidator : AbstractValidator<AceitarConviteRequest>
{
    public AceitarConviteRequestValidator()
    {
        RuleFor(x => x.PersonagemId).NotEmpty().WithMessage("Escolha um personagem.");
    }
}

public class SessaoRequestValidator : AbstractValidator<SessaoRequest>
{
    public SessaoRequestValidator()
    {
        RuleFor(x => x.Ativa).NotNull().WithMessage("Informe se a sessão fica ativa.");
    }
}
