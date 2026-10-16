using FluentValidation;

namespace Vtt.Application.Auth;

public class RegistrarRequestValidator : AbstractValidator<RegistrarRequest>
{
    public const int SenhaTamanhoMinimo = 8;
    public const int NomeExibicaoTamanhoMaximo = 40;

    public RegistrarRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Informe o e-mail.")
            .EmailAddress().WithMessage("E-mail inválido.")
            .MaximumLength(256).WithMessage("O e-mail deve ter até 256 caracteres.");

        RuleFor(x => x.Senha)
            .NotEmpty().WithMessage("Informe a senha.")
            .MinimumLength(SenhaTamanhoMinimo)
            .WithMessage($"A senha deve ter pelo menos {SenhaTamanhoMinimo} caracteres.")
            .MaximumLength(128).WithMessage("A senha deve ter até 128 caracteres.");

        RuleFor(x => x.NomeExibicao)
            .Must(nome => !string.IsNullOrWhiteSpace(nome)).WithMessage("Informe o nome de exibição.")
            .MaximumLength(NomeExibicaoTamanhoMaximo)
            .WithMessage($"O nome de exibição deve ter até {NomeExibicaoTamanhoMaximo} caracteres.");
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Informe o e-mail.");
        RuleFor(x => x.Senha).NotEmpty().WithMessage("Informe a senha.");
    }
}
