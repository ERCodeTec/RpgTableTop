using Vtt.Domain.Mesas;
using Vtt.Domain.Personagens;

namespace Vtt.Application.Mesas;

public record MesaRequest(string? Nome, string? Descricao);

public record DefinirPersonagemRequest(Guid PersonagemId);

public record SessaoRequest(bool? Ativa);

public record MovimentacaoRequest(ModoDeMovimentacao? Modo);

/// <param name="HpMaximo">
/// Só na lista que o Mestre usa para escolher o personagem de um jogador. No lobby vem nulo: é o
/// mesmo número do token do jogador, que o Mestre pode ter escolhido não mostrar ao grupo.
/// </param>
public record PersonagemResumoDto(Guid Id, string Nome, ClasseDnd Classe, int Nivel, int? HpMaximo, string CorBorda)
{
    public static PersonagemResumoDto De(Personagem personagem) => new(
        personagem.Id, personagem.Nome, personagem.Classe, personagem.Nivel, personagem.HpMaximo, personagem.CorBorda);

    /// <summary>Para o lobby e os eventos que vão à mesa inteira.</summary>
    public static PersonagemResumoDto ParaOLobby(Personagem personagem) => De(personagem) with { HpMaximo = null };
}

public record MembroDto(
    Guid UsuarioId,
    string NomeExibicao,
    PapelNaMesa Papel,
    PersonagemResumoDto? PersonagemAtivo,
    DateTimeOffset EntrouEm)
{
    public static MembroDto De(MesaMembro membro, string nomeExibicao) => new(
        membro.UsuarioId,
        nomeExibicao,
        membro.Papel,
        membro.PersonagemAtivo is null ? null : PersonagemResumoDto.ParaOLobby(membro.PersonagemAtivo),
        membro.EntrouEm);
}

public record MesaResumoDto(
    Guid Id,
    string Nome,
    string? Descricao,
    bool SessaoAtiva,
    PapelNaMesa MeuPapel,
    int QuantidadeMembros,
    DateTimeOffset CriadaEm);

public record MesaDetalheDto(
    Guid Id,
    string Nome,
    string? Descricao,
    bool SessaoAtiva,
    ModoDeMovimentacao Movimentacao,
    PapelNaMesa MeuPapel,
    DateTimeOffset CriadaEm,
    IReadOnlyList<MembroDto> Membros);
