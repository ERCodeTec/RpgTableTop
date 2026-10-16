using Vtt.Domain.Mapas;
using Vtt.Domain.Personagens;

namespace Vtt.Domain.Biblioteca;

/// <summary>
/// Um monstro ou NPC guardado na biblioteca do usuário, para colocar em qualquer mapa de qualquer mesa
/// onde ele seja Mestre. Colocar cria tokens independentes: mudar o modelo depois não os altera.
/// </summary>
public class ModeloDeToken
{
    public const int LimitePorUsuario = 500;

    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }

    /// <summary>Nulo: "Sem categoria".</summary>
    public Guid? CategoriaId { get; set; }

    /// <summary>Inimigo ou Npc; o token de personagem nasce do personagem do jogador, não daqui.</summary>
    public TipoToken Tipo { get; set; }

    public string Nome { get; set; } = string.Empty;
    public int HpMaximo { get; set; }
    public string Icone { get; set; } = string.Empty;

    /// <summary>Uma imagem pessoal do mesmo usuário; ao colocar, o token usa a cópia dela na mesa.</summary>
    public Guid? ImagemId { get; set; }

    public string CorBorda { get; set; } = Token.CorBordaPadrao;
    public int Deslocamento { get; set; } = Personagem.DeslocamentoPadrao;
    public int Visao { get; set; }
    public ExibicaoHp ExibicaoHp { get; set; } = ExibicaoHp.Barra;
    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset AtualizadoEm { get; set; }

    public static bool TipoValido(TipoToken tipo) => tipo is TipoToken.Inimigo or TipoToken.Npc;

    /// <summary>Um token novo com os dados do modelo e o HP cheio.</summary>
    /// <param name="imagemDaMesaId">A cópia da imagem do modelo na mesa do mapa, se ele tiver imagem.</param>
    public Token CriarToken(Guid mapaId, string nome, int x, int y, Guid? imagemDaMesaId, bool visivel) => new()
    {
        Id = Guid.NewGuid(),
        MapaId = mapaId,
        Tipo = Tipo,
        Nome = nome,
        HpAtual = HpMaximo,
        HpMaximo = HpMaximo,
        X = x,
        Y = y,
        CorBorda = CorBorda,
        Icone = Icone,
        ImagemId = imagemDaMesaId,
        Visivel = visivel,
        ExibicaoHp = ExibicaoHp,
        Deslocamento = Deslocamento,
        MovimentoRestante = Deslocamento,
        Visao = Visao
    };
}
