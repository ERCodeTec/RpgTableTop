namespace Vtt.Domain.Mesas;

public class Mesa
{
    public const int NomeTamanhoMaximo = 80;
    public const int DescricaoTamanhoMaximo = 500;

    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public Guid CriadaPorUsuarioId { get; set; }

    /// <summary>Com a sessão ativa, jogadores não trocam o próprio personagem.</summary>
    public bool SessaoAtiva { get; set; }

    public ModoDeMovimentacao Movimentacao { get; set; } = ModoDeMovimentacao.PorBlocos;

    public EstadoDaIniciativa Iniciativa { get; set; } = EstadoDaIniciativa.Inativa;

    /// <summary>A rodada do combate, a partir de 1; 0 fora dele.</summary>
    public int Rodada { get; set; }

    /// <summary>O token que está na vez, no mapa ativo; nulo fora do combate ou sem ninguém que possa jogar.</summary>
    public Guid? VezDoTokenId { get; set; }

    public DateTimeOffset CriadaEm { get; set; }

    public List<MesaMembro> Membros { get; set; } = [];
    public List<Convite> Convites { get; set; } = [];
}
