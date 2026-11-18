using Vtt.Domain.Mapas;
using Vtt.Domain.Personagens;

namespace Vtt.Application.Personagens;

/// <param name="Deslocamento">Em células por rodada; sem ele, o padrão (6 = 30 ft).</param>
/// <param name="Visao">Em células, com a névoa ligada; sem ela, o padrão (12 = 60 ft).</param>
/// <param name="Tamanho">A categoria de tamanho; sem ela, Médio.</param>
/// <param name="LadoGargantuesco">Só no Gargantuesco: o lado em células (4 a 10); sem ele, 4.</param>
public record PersonagemRequest(
    string? Nome, ClasseDnd? Classe, int Nivel, int HpMaximo, string? CorBorda, int Deslocamento = Personagem.DeslocamentoPadrao,
    int Visao = Personagem.VisaoPadrao, TamanhoDeCriatura Tamanho = Tamanhos.Padrao, int? LadoGargantuesco = null);

public record PersonagemDto(
    Guid Id,
    string Nome,
    ClasseDnd Classe,
    int Nivel,
    int HpMaximo,
    string CorBorda,
    int Deslocamento,
    int Visao,
    Guid? ImagemId,
    DateTimeOffset CriadoEm,
    DateTimeOffset AtualizadoEm,
    TamanhoDeCriatura Tamanho = Tamanhos.Padrao,
    int? LadoGargantuesco = null)
{
    public static PersonagemDto De(Personagem personagem) => new(
        personagem.Id,
        personagem.Nome,
        personagem.Classe,
        personagem.Nivel,
        personagem.HpMaximo,
        personagem.CorBorda,
        personagem.Deslocamento,
        personagem.Visao,
        personagem.ImagemId,
        personagem.CriadoEm,
        personagem.AtualizadoEm,
        personagem.Tamanho,
        personagem.LadoGargantuesco);
}
