using FluentValidation;
using Vtt.Domain.Personagens;

namespace Vtt.Application.Personagens;

public class PersonagemRequestValidator : AbstractValidator<PersonagemRequest>
{
    public PersonagemRequestValidator()
    {
        RuleFor(x => x.Nome)
            .Must(nome => !string.IsNullOrWhiteSpace(nome)).WithMessage("Informe o nome do personagem.")
            .Must(nome => nome is null || nome.Trim().Length <= Personagem.NomeTamanhoMaximo)
            .WithMessage($"O nome deve ter até {Personagem.NomeTamanhoMaximo} caracteres.");

        RuleFor(x => x.Classe)
            .NotNull().WithMessage("Escolha a classe.")
            .IsInEnum().WithMessage("Classe inválida.");

        RuleFor(x => x.Nivel)
            .InclusiveBetween(Personagem.NivelMinimo, Personagem.NivelMaximo)
            .WithMessage($"O nível deve ficar entre {Personagem.NivelMinimo} e {Personagem.NivelMaximo}.");

        RuleFor(x => x.HpMaximo)
            .InclusiveBetween(Personagem.HpMaximoMinimo, Personagem.HpMaximoLimite)
            .WithMessage($"O HP máximo deve ficar entre {Personagem.HpMaximoMinimo} e {Personagem.HpMaximoLimite}.");

        RuleFor(x => x.Deslocamento)
            .InclusiveBetween(Personagem.DeslocamentoMinimo, Personagem.DeslocamentoLimite)
            .WithMessage($"O deslocamento deve ficar entre {Personagem.DeslocamentoMinimo} e {Personagem.DeslocamentoLimite} células.");

        RuleFor(x => x.Visao)
            .InclusiveBetween(Personagem.VisaoMinima, Personagem.VisaoLimite)
            .WithMessage($"A visão deve ficar entre {Personagem.VisaoMinima} e {Personagem.VisaoLimite} células.");

        RuleFor(x => x.CorBorda)
            .Matches("^#[0-9A-Fa-f]{6}$").WithMessage("A cor da borda deve estar no formato #RRGGBB.")
            .When(x => !string.IsNullOrEmpty(x.CorBorda));
    }
}
