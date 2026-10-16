using FluentValidation;
using Vtt.Application.Mapas;
using Vtt.Domain.Biblioteca;
using Vtt.Domain.Mapas;
using Vtt.Domain.Personagens;

namespace Vtt.Application.Biblioteca;

public class CategoriaRequestValidator : AbstractValidator<CategoriaRequest>
{
    public CategoriaRequestValidator()
    {
        RuleFor(x => x.Nome)
            .Must(nome => !string.IsNullOrWhiteSpace(nome)).WithMessage("Informe o nome da categoria.")
            .Must(nome => nome is null || nome.Trim().Length <= CategoriaDeModelo.NomeTamanhoMaximo)
            .WithMessage($"O nome deve ter até {CategoriaDeModelo.NomeTamanhoMaximo} caracteres.");
    }
}

public class ModeloDeTokenRequestValidator : AbstractValidator<ModeloDeTokenRequest>
{
    public ModeloDeTokenRequestValidator()
    {
        RuleFor(x => x.Tipo)
            .NotNull().WithMessage("Escolha o tipo.")
            .Must(tipo => tipo is null || ModeloDeToken.TipoValido(tipo.Value))
            .WithMessage("O modelo deve ser um inimigo, um NPC ou uma interação.");

        RuleFor(x => x.Nome).NomeDeToken();
        RuleFor(x => x.Descricao).DescricaoDeInteracao();

        // A interação não tem HP, deslocamento nem visão: o serviço grava os valores fixos dela.
        When(x => x.Tipo != TipoToken.Interacao, () =>
        {
            RuleFor(x => x.HpMaximo).HpMaximoDeToken();

            RuleFor(x => x.Deslocamento)
                .InclusiveBetween(Personagem.DeslocamentoMinimo, Personagem.DeslocamentoLimite)
                .WithMessage($"O deslocamento deve ficar entre {Personagem.DeslocamentoMinimo} e {Personagem.DeslocamentoLimite} células.");

            RuleFor(x => x.Visao)
                .InclusiveBetween(Personagem.VisaoMinima, Personagem.VisaoLimite)
                .WithMessage($"A visão deve ficar entre {Personagem.VisaoMinima} e {Personagem.VisaoLimite} células.");
        });

        RuleFor(x => x.Icone)
            .Must((request, icone) => request.Tipo is not { } tipo || !ModeloDeToken.TipoValido(tipo) || Token.IconeValido(tipo, icone))
            .WithMessage(RegrasDeToken.IconeInvalido);

        RuleFor(x => x.ExibicaoHp)
            .IsInEnum().WithMessage("Modo de exibição do HP inválido.");

        RuleFor(x => x.CorBorda).CorDeToken();
    }
}
