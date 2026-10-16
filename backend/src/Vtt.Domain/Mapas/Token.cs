using Vtt.Domain.Personagens;

namespace Vtt.Domain.Mapas;

public class Token
{
    public const int NomeTamanhoMaximo = 60;
    public const int IconeTamanhoMaximo = 30;
    public const int HpMaximoMinimo = 1;
    public const int HpMaximoLimite = 999;
    public const string CorBordaPadrao = "#C0503C";

    public Guid Id { get; set; }
    public Guid MapaId { get; set; }
    public TipoToken Tipo { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int HpAtual { get; set; }
    public int HpMaximo { get; set; }

    /// <summary>Célula do mapa, a partir de (0,0) no canto superior esquerdo.</summary>
    public int X { get; set; }

    public int Y { get; set; }
    public string CorBorda { get; set; } = CorBordaPadrao;

    /// <summary>Nome da classe de D&amp;D para tokens de personagem; chave de <see cref="IconesGenericos"/> para os demais.</summary>
    public string Icone { get; set; } = string.Empty;

    /// <summary>Com imagem, o disco mostra a imagem no lugar do ícone.</summary>
    public Guid? ImagemId { get; set; }

    public Guid? PersonagemId { get; set; }

    /// <summary>Preenchido só no token que o servidor cria para o jogador; é quem pode movê-lo.</summary>
    public Guid? DonoUsuarioId { get; set; }

    /// <summary>
    /// false: o token existe só para o Mestre e para o dono. Nada dele é enviado aos outros
    /// jogadores, e para eles a célula conta como vazia.
    /// </summary>
    public bool Visivel { get; set; } = true;

    public ExibicaoHp ExibicaoHp { get; set; } = ExibicaoHp.Barra;

    /// <summary>
    /// Células por rodada no movimento por blocos. No token de jogador, espelha o personagem;
    /// nos demais, o Mestre define.
    /// </summary>
    public int Deslocamento { get; set; } = Personagem.DeslocamentoPadrao;

    /// <summary>
    /// Até onde o token enxerga, em células, quando o mapa tem névoa. No token de jogador, espelha o
    /// personagem; nos demais, maior que 0 marca um aliado cuja visão entra na dos jogadores.
    /// </summary>
    public int Visao { get; set; }

    /// <summary>O que ainda dá para andar nesta rodada. Volta ao <see cref="Deslocamento"/> na nova rodada.</summary>
    public int MovimentoRestante { get; set; } = Personagem.DeslocamentoPadrao;

    /// <summary>
    /// A Disparada desta rodada já foi usada: ela soma mais um <see cref="Deslocamento"/> ao
    /// movimento, uma vez por rodada. Volta a false na nova rodada.
    /// </summary>
    public bool Disparada { get; set; }

    /// <summary>O movimento inteiro da rodada: o deslocamento, dobrado pela Disparada.</summary>
    public int MovimentoMaximo => MaximoCom(Deslocamento);

    /// <summary>
    /// De onde saiu o último movimento por blocos ainda não concluído; nulo sem pendência.
    /// Desfazer volta o token para cá e devolve <see cref="CustoPendente"/>.
    /// </summary>
    public int? OrigemPendenteX { get; set; }

    public int? OrigemPendenteY { get; set; }

    public int CustoPendente { get; set; }

    public bool MovimentoPendente => OrigemPendenteX is not null;

    /// <summary>A vez deste token na iniciativa já passou nesta rodada. Volta a false na rodada seguinte.</summary>
    public bool JogouNaRodada { get; set; }

    /// <summary>Incrementada a cada alteração; o cliente a envia de volta ao mover.</summary>
    public int Versao { get; set; } = 1;

    /// <summary>Descarta o movimento pendente: ele não pode mais ser desfeito, e o gasto fica.</summary>
    public void ConcluirMovimento()
    {
        OrigemPendenteX = null;
        OrigemPendenteY = null;
        CustoPendente = 0;
    }

    /// <summary>Muda o deslocamento mantendo o que já foi gasto na rodada, sem sair de 0 ao novo máximo.</summary>
    public void DefinirDeslocamento(int novo)
    {
        var maximo = MaximoCom(novo);
        MovimentoRestante = Math.Clamp(MovimentoRestante + maximo - MovimentoMaximo, 0, maximo);
        Deslocamento = novo;
    }

    /// <summary>Usa a Disparada da rodada: mais um deslocamento inteiro para andar.</summary>
    public void Disparar()
    {
        Disparada = true;
        MovimentoRestante += Deslocamento;
    }

    /// <summary>A Disparada só volta atrás enquanto o deslocamento extra não foi gasto.</summary>
    public bool PodeDesfazerDisparada => Disparada && MovimentoRestante >= Deslocamento;

    /// <summary>Devolve a Disparada da rodada: o movimento perde o deslocamento que ela somou.</summary>
    public void DesfazerDisparada()
    {
        Disparada = false;
        MovimentoRestante -= Deslocamento;
    }

    private int MaximoCom(int deslocamento) => Disparada ? deslocamento * 2 : deslocamento;

    /// <summary>Com 0 de HP o token fica caído, mas continua no mapa.</summary>
    public bool Caido => HpAtual == 0;

    /// <summary>
    /// O HP em faixas de 5%, arredondado para cima (só é 0 quando o token está caído). É o que vai
    /// aos jogadores que veem só a barra: o percentual exato deixaria deduzir o HP de criaturas fracas.
    /// </summary>
    public int HpPercentual => HpAtual == 0 ? 0 : Math.Min(100, (int)Math.Ceiling(HpAtual * 20.0 / HpMaximo) * 5);

    /// <summary>Personagens mostram o HP completo ao grupo; inimigos e NPCs, só a barra.</summary>
    public static ExibicaoHp ExibicaoHpPadrao(TipoToken tipo) =>
        tipo == TipoToken.Personagem ? ExibicaoHp.Completo : ExibicaoHp.Barra;

    public static bool IconeValido(TipoToken tipo, string? icone) =>
        icone is not null && (tipo == TipoToken.Personagem
            ? Enum.GetNames<ClasseDnd>().Contains(icone)
            : IconesGenericos.Chaves.Contains(icone));
}
