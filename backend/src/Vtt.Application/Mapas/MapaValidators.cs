using FluentValidation;
using Vtt.Domain.Mapas;
using Vtt.Domain.Personagens;

namespace Vtt.Application.Mapas;

public class MapaRequestValidator : AbstractValidator<MapaRequest>
{
    public MapaRequestValidator()
    {
        RuleFor(x => x.Nome)
            .Must(nome => !string.IsNullOrWhiteSpace(nome)).WithMessage("Informe o nome do mapa.")
            .Must(nome => nome is null || nome.Trim().Length <= Mapa.NomeTamanhoMaximo)
            .WithMessage($"O nome deve ter até {Mapa.NomeTamanhoMaximo} caracteres.");

        RuleFor(x => x.Largura)
            .InclusiveBetween(Mapa.DimensaoMinima, Mapa.DimensaoMaxima)
            .WithMessage($"A largura deve ficar entre {Mapa.DimensaoMinima} e {Mapa.DimensaoMaxima} células.");

        RuleFor(x => x.Altura)
            .InclusiveBetween(Mapa.DimensaoMinima, Mapa.DimensaoMaxima)
            .WithMessage($"A altura deve ficar entre {Mapa.DimensaoMinima} e {Mapa.DimensaoMaxima} células.");

        RuleFor(x => x.CorFundo)
            .Matches(RegrasDeToken.CorHex).WithMessage("A cor de fundo deve estar no formato #RRGGBB.")
            .When(x => !string.IsNullOrEmpty(x.CorFundo));
    }
}

public class MapaProceduralRequestValidator : AbstractValidator<MapaProceduralRequest>
{
    public MapaProceduralRequestValidator()
    {
        RuleFor(x => x.Nome)
            .Must(nome => nome!.Trim().Length <= Mapa.NomeTamanhoMaximo)
            .WithMessage($"O nome deve ter até {Mapa.NomeTamanhoMaximo} caracteres.")
            .When(x => x.Nome is not null);

        RuleFor(x => x.Largura)
            .InclusiveBetween(GeradorDeMasmorra.DimensaoMinima, Mapa.DimensaoMaxima)
            .WithMessage($"A largura deve ficar entre {GeradorDeMasmorra.DimensaoMinima} e {Mapa.DimensaoMaxima} células.");

        RuleFor(x => x.Altura)
            .InclusiveBetween(GeradorDeMasmorra.DimensaoMinima, Mapa.DimensaoMaxima)
            .WithMessage($"A altura deve ficar entre {GeradorDeMasmorra.DimensaoMinima} e {Mapa.DimensaoMaxima} células.");

        RuleFor(x => x.Semente).GreaterThanOrEqualTo(0).WithMessage("A semente não pode ser negativa.");

        RuleFor(x => x.Tema)
            .Must(tema => GeradorDeMasmorra.TemaValido(tema!))
            .WithMessage("Tema de mapa desconhecido.")
            .When(x => x.Tema is not null);
    }
}

public class FundoRequestValidator : AbstractValidator<FundoRequest>
{
    /// <summary>Folga, em células, para a imagem sair um pouco do mapa sem se perder de vista.</summary>
    public const double OffsetMaximo = 500;

    public FundoRequestValidator()
    {
        RuleFor(x => x.Escala)
            .InclusiveBetween(Mapa.FundoEscalaMinima, Mapa.FundoEscalaMaxima)
            .WithMessage($"A escala deve ficar entre {Mapa.FundoEscalaMinima} e {Mapa.FundoEscalaMaxima}.");

        RuleFor(x => x.OffsetX).InclusiveBetween(-OffsetMaximo, OffsetMaximo).WithMessage("Deslocamento fora da faixa.");
        RuleFor(x => x.OffsetY).InclusiveBetween(-OffsetMaximo, OffsetMaximo).WithMessage("Deslocamento fora da faixa.");
    }
}

public class EstruturaRequestValidator : AbstractValidator<EstruturaRequest>
{
    public EstruturaRequestValidator()
    {
        RuleFor(x => x.Tipo)
            .NotNull().WithMessage("Escolha o tipo da estrutura.")
            .IsInEnum().WithMessage("Tipo inválido.");

        RuleFor(x => x.X).GreaterThanOrEqualTo(0).WithMessage("A estrutura fica fora do mapa.");
        RuleFor(x => x.Y).GreaterThanOrEqualTo(0).WithMessage("A estrutura fica fora do mapa.");
        RuleFor(x => x.Largura).InclusiveBetween(1, Mapa.DimensaoMaxima).WithMessage("A largura deve ter pelo menos 1 célula.");
        RuleFor(x => x.Profundidade).InclusiveBetween(1, Mapa.DimensaoMaxima).WithMessage("A profundidade deve ter pelo menos 1 célula.");

        RuleFor(x => x.Altura)
            .InclusiveBetween(Estrutura.AlturaMinima, Estrutura.AlturaMaxima)
            .WithMessage($"A altura deve ficar entre {Estrutura.AlturaMinima} e {Estrutura.AlturaMaxima} células.");

        RuleFor(x => x.RotacaoGraus)
            .Must(Estrutura.RotacaoValida).WithMessage("A rotação deve ser 0, 90, 180 ou 270 graus.");

        RuleFor(x => x.Cor)
            .Matches(RegrasDeToken.CorHex).WithMessage("A cor deve estar no formato #RRGGBB.")
            .When(x => !string.IsNullOrEmpty(x.Cor));

        RuleFor(x => x.TexturaPadrao)
            .Must(chave => TexturasPadrao.Chaves.Contains(chave!)).WithMessage("Textura pronta desconhecida.")
            .When(x => x.TexturaPadrao is not null);

        RuleFor(x => x.TexturaPadrao)
            .Null().WithMessage("Escolha uma textura pronta ou uma imagem da galeria, não as duas.")
            .When(x => x.TexturaImagemId is not null);
    }
}

public class HpRequestValidator : AbstractValidator<HpRequest>
{
    public HpRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.Delta is not null || x.HpAtual is not null || x.HpMaximo is not null)
            .WithName("delta")
            .WithMessage("Informe o dano, a cura, o HP atual ou o HP máximo.");

        RuleFor(x => x)
            .Must(x => x.Delta is null || x.HpAtual is null)
            .WithName("delta")
            .WithMessage("Informe o dano/cura ou o HP atual, não os dois.");

        RuleFor(x => x.Delta)
            .InclusiveBetween(-Token.HpMaximoLimite, Token.HpMaximoLimite)
            .WithMessage($"O dano ou a cura deve ficar entre -{Token.HpMaximoLimite} e {Token.HpMaximoLimite}.");

        RuleFor(x => x.HpAtual)
            .InclusiveBetween(0, Token.HpMaximoLimite)
            .WithMessage($"O HP atual deve ficar entre 0 e {Token.HpMaximoLimite}.");

        RuleFor(x => x.HpMaximo)
            .InclusiveBetween(Token.HpMaximoMinimo, Token.HpMaximoLimite)
            .WithMessage($"O HP máximo deve ficar entre {Token.HpMaximoMinimo} e {Token.HpMaximoLimite}.");
    }
}

public class ExibicaoRequestValidator : AbstractValidator<ExibicaoRequest>
{
    public ExibicaoRequestValidator()
    {
        RuleFor(x => x.ExibicaoHp).IsInEnum().WithMessage("Modo de exibição inválido.");
    }
}

public class PortaRequestValidator : AbstractValidator<PortaRequest>
{
    public PortaRequestValidator()
    {
        RuleFor(x => x.Aberta).NotNull().WithMessage("Informe se a porta fica aberta.");
    }
}

public class CriarTokenRequestValidator : AbstractValidator<CriarTokenRequest>
{
    public CriarTokenRequestValidator()
    {
        RuleFor(x => x.Tipo)
            .NotNull().WithMessage("Escolha o tipo do token.")
            .IsInEnum().WithMessage("Tipo inválido.");

        RuleFor(x => x.Nome).NomeDeToken();
        RuleFor(x => x.HpMaximo).HpMaximoDeToken();
        RuleFor(x => x.HpAtual).HpAtualDeToken(x => x.HpMaximo);
        RuleFor(x => x.CorBorda).CorDeToken();

        RuleFor(x => x.Icone)
            .Must((request, icone) => Token.IconeValido(request.Tipo!.Value, icone))
            .WithMessage(RegrasDeToken.IconeInvalido)
            .When(x => x.Tipo is not null && Enum.IsDefined(x.Tipo.Value));

        RuleFor(x => x.Deslocamento).DeslocamentoDeToken();
        RuleFor(x => x.Visao).VisaoDeToken();

        RuleFor(x => x.X)
            .Must((request, x) => (x is null) == (request.Y is null)).WithMessage("Informe X e Y juntos, ou nenhum dos dois.");
    }
}

public class EditarTokenRequestValidator : AbstractValidator<EditarTokenRequest>
{
    public EditarTokenRequestValidator()
    {
        RuleFor(x => x.Nome).NomeDeToken();
        RuleFor(x => x.HpMaximo).HpMaximoDeToken();
        RuleFor(x => x.HpAtual).HpAtualDeToken(x => x.HpMaximo);
        RuleFor(x => x.CorBorda).CorDeToken();
        RuleFor(x => x.Icone).NotEmpty().WithMessage(RegrasDeToken.IconeInvalido);
        RuleFor(x => x.ExibicaoHp).IsInEnum().WithMessage("Modo de exibição inválido.");
        RuleFor(x => x.Deslocamento).DeslocamentoDeToken();
        RuleFor(x => x.Visao).VisaoDeToken();
    }
}

public class CriarDaBibliotecaRequestValidator : AbstractValidator<CriarDaBibliotecaRequest>
{
    public CriarDaBibliotecaRequestValidator()
    {
        RuleFor(x => x.ModeloId).NotNull().WithMessage("Escolha um modelo da biblioteca.");
        RuleFor(x => x.Quantidade)
            .InclusiveBetween(1, CriarDaBibliotecaRequest.QuantidadeMaxima)
            .WithMessage($"A quantidade deve ficar entre 1 e {CriarDaBibliotecaRequest.QuantidadeMaxima}.");
    }
}

public class NevoaRequestValidator : AbstractValidator<NevoaRequest>
{
    public NevoaRequestValidator()
    {
        RuleFor(x => x.Modo)
            .NotNull().WithMessage("Escolha o modo da névoa.")
            .IsInEnum().WithMessage("Modo de névoa inválido.");
    }
}

public class RevelarCelulasRequestValidator : AbstractValidator<RevelarCelulasRequest>
{
    public RevelarCelulasRequestValidator()
    {
        RuleFor(x => x.Celulas)
            .NotEmpty().WithMessage("Pinte ao menos uma célula.")
            .Must(celulas => celulas is null || celulas.Length <= MapaDeBits.Tamanho(Mapa.DimensaoMaxima * Mapa.DimensaoMaxima))
            .WithMessage("Camada maior que o maior mapa possível.");
    }
}

internal static class RegrasDeToken
{
    public const string CorHex = "^#[0-9A-Fa-f]{6}$";
    public const string IconeInvalido = "Escolha um ícone válido para o tipo do token.";

    public static IRuleBuilderOptions<T, string?> NomeDeToken<T>(this IRuleBuilder<T, string?> regra) => regra
        .Must(nome => !string.IsNullOrWhiteSpace(nome)).WithMessage("Informe o nome do token.")
        .Must(nome => nome is null || nome.Trim().Length <= Token.NomeTamanhoMaximo)
        .WithMessage($"O nome deve ter até {Token.NomeTamanhoMaximo} caracteres.");

    public static IRuleBuilderOptions<T, int> HpMaximoDeToken<T>(this IRuleBuilder<T, int> regra) => regra
        .InclusiveBetween(Token.HpMaximoMinimo, Token.HpMaximoLimite)
        .WithMessage($"O HP máximo deve ficar entre {Token.HpMaximoMinimo} e {Token.HpMaximoLimite}.");

    public static IRuleBuilderOptions<T, int> HpAtualDeToken<T>(this IRuleBuilder<T, int> regra, Func<T, int> hpMaximo) => regra
        .Must((request, hpAtual) => hpAtual >= 0 && hpAtual <= hpMaximo(request))
        .WithMessage("O HP atual deve ficar entre 0 e o HP máximo.");

    public static IRuleBuilderOptions<T, int?> DeslocamentoDeToken<T>(this IRuleBuilder<T, int?> regra) => regra
        .InclusiveBetween(Personagem.DeslocamentoMinimo, Personagem.DeslocamentoLimite)
        .WithMessage($"O deslocamento deve ficar entre {Personagem.DeslocamentoMinimo} e {Personagem.DeslocamentoLimite} células.");

    public static IRuleBuilderOptions<T, int?> VisaoDeToken<T>(this IRuleBuilder<T, int?> regra) => regra
        .InclusiveBetween(Personagem.VisaoMinima, Personagem.VisaoLimite)
        .WithMessage($"A visão deve ficar entre {Personagem.VisaoMinima} e {Personagem.VisaoLimite} células.");

    public static IRuleBuilderOptions<T, string?> CorDeToken<T>(this IRuleBuilder<T, string?> regra) => regra
        .Must(cor => string.IsNullOrEmpty(cor) || System.Text.RegularExpressions.Regex.IsMatch(cor, CorHex))
        .WithMessage("A cor da borda deve estar no formato #RRGGBB.");
}
