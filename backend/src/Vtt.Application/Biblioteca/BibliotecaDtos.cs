using Vtt.Domain.Biblioteca;
using Vtt.Domain.Mapas;
using Vtt.Domain.Personagens;

namespace Vtt.Application.Biblioteca;

public record CategoriaRequest(string? Nome);

public record CategoriaDto(Guid Id, string Nome, int QuantidadeDeModelos);

/// <param name="CategoriaId">Nulo: "Sem categoria".</param>
/// <param name="ImagemId">Uma imagem da galeria pessoal (/api/biblioteca/imagens).</param>
/// <param name="ExibicaoHp">Nulo: ao criar, o padrão do tipo; ao editar, mantém o atual.</param>
/// <param name="Descricao">Só na interação: o texto mostrado a quem examinar o objeto.</param>
/// <param name="ConteudoImagemId">Só na interação: uma imagem da galeria pessoal mostrada a quem examinar.</param>
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
    ExibicaoHp? ExibicaoHp = null,
    string? Descricao = null,
    Guid? ConteudoImagemId = null);

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
    DateTimeOffset AtualizadoEm,
    string? Descricao,
    Guid? ConteudoImagemId)
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
        modelo.AtualizadoEm,
        modelo.Descricao,
        modelo.ConteudoImagemId);
}
