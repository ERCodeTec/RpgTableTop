using Vtt.Domain.Mapas;

namespace Vtt.Application.Mapas;

/// <param name="Geravel">Se o gerador de mapas sabe montar um mapa com este tema.</param>
public record TemaDeTexturaDto(string Id, string Nome, bool Geravel);

/// <summary>As texturas prontas com as categorias, para o seletor do Mestre, o guia e o gerador.</summary>
public record CatalogoDeTexturasDto(IReadOnlyList<TemaDeTexturaDto> Temas, IReadOnlyList<TexturaPronta> Texturas)
{
    public static readonly CatalogoDeTexturasDto Atual = new(
        TexturasPadrao.Temas.Select(t => new TemaDeTexturaDto(t.Id, t.Nome, GeradorDeMasmorra.TemaValido(t.Id))).ToList(),
        TexturasPadrao.Todas);
}
