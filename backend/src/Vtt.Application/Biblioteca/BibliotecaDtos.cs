using Vtt.Domain.Biblioteca;
using Vtt.Domain.Mapas;
using Vtt.Domain.Personagens;

namespace Vtt.Application.Biblioteca;

public record CategoriaRequest(string? Nome);

public record CategoriaDto(Guid Id, string Nome, int QuantidadeDeModelos);

/// <param name="CategoriaId">Nulo: "Sem categoria".</param>
/// <param name="ImagemId">Uma imagem da galeria pessoal (/api/biblioteca/imagens).</param>
/// <param name="ExibicaoHp">Nulo: ao criar, o padrão do tipo; ao editar, mantém o atual.</param>
public record ModeloDeTokenRequest(
    Guid? CategoriaId,
    TipoToken? Tipo,
    string? Nome,
    int HpMaximo,
    string? Icone,
    Guid? ImagemId,
    string? CorBorda,
    int Deslocamento = Personagem.DeslocamentoPadrao,
    int Visao = 0,
    ExibicaoHp? ExibicaoHp = null);

public record ModeloDeTokenDto(
    Guid Id,
    Guid? CategoriaId,
    TipoToken Tipo,
    string Nome,
    int HpMaximo,
    string Icone,
    Guid? ImagemId,
    string CorBorda,
    int Deslocamento,
    int Visao,
    ExibicaoHp ExibicaoHp,
    DateTimeOffset CriadoEm,
    DateTimeOffset AtualizadoEm)
{
    public static ModeloDeTokenDto De(ModeloDeToken modelo) => new(
        modelo.Id,
        modelo.CategoriaId,
        modelo.Tipo,
        modelo.Nome,
        modelo.HpMaximo,
        modelo.Icone,
        modelo.ImagemId,
        modelo.CorBorda,
        modelo.Deslocamento,
        modelo.Visao,
        modelo.ExibicaoHp,
        modelo.CriadoEm,
        modelo.AtualizadoEm);
}
