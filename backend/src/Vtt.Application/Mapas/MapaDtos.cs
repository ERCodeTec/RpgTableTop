using Vtt.Domain.Mapas;
using Vtt.Domain.Mesas;

namespace Vtt.Application.Mapas;

public record MapaRequest(string? Nome, int Largura, int Altura, string? CorFundo);

/// <summary>Pede uma masmorra gerada. Campos nulos são escolhidos pelo servidor; a semente, ao acaso.</summary>
/// <param name="Semente">A mesma semente, com as mesmas dimensões, gera sempre o mesmo mapa.</param>
/// <param name="Tema">Um de <see cref="GeradorDeMasmorra.Temas"/>; sem ele, masmorra.</param>
public record MapaProceduralRequest(string? Nome = null, int? Largura = null, int? Altura = null, int? Semente = null, string? Tema = null);

/// <param name="X">Sem X e Y, o token nasce na primeira célula livre a partir do canto superior esquerdo.</param>
public record CriarTokenRequest(
    TipoToken? Tipo,
    string? Nome,
    int HpAtual,
    int HpMaximo,
    string? Icone,
    string? CorBorda,
    int? X,
    int? Y,
    Guid? ImagemId = null,
    int? Deslocamento = null,
    int? Visao = null);

/// <summary>Coloca cópias de um modelo da biblioteca nas primeiras células livres do mapa.</summary>
/// <param name="Quantidade">Com mais de uma, os nomes ganham número ("Goblin 1", "Goblin 2"…), continuando os que já existem no mapa.</param>
/// <param name="Visivel">false cria os tokens ocultos dos jogadores; nulo, visíveis.</param>
public record CriarDaBibliotecaRequest(Guid? ModeloId, int Quantidade = 1, bool? Visivel = null)
{
    public const int QuantidadeMaxima = 20;
}

/// <param name="ImagemId">A imagem do token; null remove a que houver.</param>
/// <param name="ExibicaoHp">null mantém o modo de exibição atual.</param>
/// <param name="Deslocamento">null mantém o atual. Ignorado no token de jogador, que usa o do personagem.</param>
/// <param name="Visao">null mantém a atual. Ignorada no token de jogador, que usa a do personagem.</param>
public record EditarTokenRequest(
    string? Nome, int HpAtual, int HpMaximo, string? Icone, string? CorBorda, bool? Visivel, Guid? ImagemId = null,
    ExibicaoHp? ExibicaoHp = null, int? Deslocamento = null, int? Visao = null);

/// <summary>
/// Altera o HP. <paramref name="Delta"/> soma ao HP atual (dano negativo, cura positiva);
/// <paramref name="HpAtual"/> define o valor. O resultado fica sempre entre 0 e o máximo.
/// </summary>
public record HpRequest(int? Delta, int? HpAtual, int? HpMaximo);

/// <summary>O que os jogadores veem do token. Campos nulos ficam como estão.</summary>
public record ExibicaoRequest(bool? Visivel, ExibicaoHp? ExibicaoHp);

/// <param name="ImagemId">null remove a imagem de fundo.</param>
/// <param name="Escala">1 = a imagem tem a largura do mapa.</param>
/// <param name="OffsetX">Deslocamento do canto superior esquerdo da imagem, em células.</param>
public record FundoRequest(Guid? ImagemId, double Escala, double OffsetX, double OffsetY);

/// <param name="Versao">Na edição, a versão que o cliente conhece; se a do servidor for outra, é Conflito.</param>
public record EstruturaRequest(
    TipoEstrutura? Tipo,
    int X,
    int Y,
    int Largura,
    int Profundidade,
    double Altura,
    int RotacaoGraus,
    string? Cor,
    Guid? TexturaImagemId,
    bool? BloqueiaMovimento,
    bool? PortaAberta,
    int? Versao = null,
    string? TexturaPadrao = null);

public record PortaRequest(bool? Aberta);

public record NevoaRequest(ModoDeNevoa? Modo);

/// <param name="Celulas">As células pintadas pelo pincel, no formato de <see cref="MapaDeBits"/> (base64 no JSON).</param>
/// <param name="Revelar">true revela as células; false as cobre, apagando também o que foi explorado nelas.</param>
public record RevelarCelulasRequest(byte[]? Celulas, bool Revelar);

/// <summary>Uma camada da névoa, no formato de <see cref="MapaDeBits"/> (base64 no JSON).</summary>
/// <param name="UsuarioId">Nulo na memória do grupo e na camada revelada; o jogador dono, na memória individual.</param>
public record MemoriaDeNevoaDto(Guid MapaId, TipoDeMemoria Tipo, Guid? UsuarioId, byte[] Celulas)
{
    public static MemoriaDeNevoaDto De(MemoriaDeNevoa memoria) =>
        new(memoria.MapaId, memoria.Tipo, memoria.UsuarioId, memoria.Celulas);

    /// <summary>Quem pode ver esta camada: o Mestre vê todas; o jogador, as comuns e a dele.</summary>
    public bool VisivelPara(Guid usuarioId, PapelNaMesa papel) =>
        papel == PapelNaMesa.Mestre || UsuarioId is null || UsuarioId == usuarioId;
}

public record MapaDto(
    Guid Id,
    Guid MesaId,
    string Nome,
    int Largura,
    int Altura,
    bool Ativo,
    string CorFundo,
    DateTimeOffset CriadoEm,
    Guid? ImagemFundoId,
    double FundoEscala,
    double FundoOffsetX,
    double FundoOffsetY,
    ModoDeNevoa Nevoa)
{
    public static MapaDto De(Mapa mapa) => new(
        mapa.Id,
        mapa.MesaId,
        mapa.Nome,
        mapa.Largura,
        mapa.Altura,
        mapa.Ativo,
        mapa.CorFundo,
        mapa.CriadoEm,
        mapa.ImagemFundoId,
        mapa.FundoEscala,
        mapa.FundoOffsetX,
        mapa.FundoOffsetY,
        mapa.Nevoa);
}

/// <summary>Para quem um token está sendo montado: é o que decide o que entra no payload.</summary>
public enum VisaoDoToken
{
    Mestre,

    /// <summary>O jogador a quem o token pertence: vê tudo dele, mesmo oculto.</summary>
    Dono,

    /// <summary>Os demais jogadores.</summary>
    Jogador
}

/// <summary>
/// Um token como um destinatário o vê. Os campos de HP vêm nulos quando ele não pode vê-los; o
/// filtro acontece aqui, no servidor, e nunca no cliente.
/// </summary>
/// <param name="HpPercentual">Em faixas de 5%. Nulo para quem não vê nem a barra.</param>
/// <param name="Deslocamento">O movimento (deslocamento, restante, pendência, disparada) só vai ao Mestre e ao dono.</param>
/// <param name="Visao">Em células; vai a todos, porque é com ela que cada cliente desenha a névoa.</param>
public record TokenDto(
    Guid Id,
    Guid MapaId,
    TipoToken Tipo,
    string Nome,
    int? HpAtual,
    int? HpMaximo,
    int X,
    int Y,
    string CorBorda,
    string Icone,
    Guid? PersonagemId,
    Guid? DonoUsuarioId,
    bool Visivel,
    int Versao,
    Guid? ImagemId,
    int? HpPercentual,
    bool Caido,
    ExibicaoHp ExibicaoHp,
    int? Deslocamento,
    int? MovimentoRestante,
    bool? MovimentoPendente,
    int Visao,
    bool? Disparada)
{
    /// <summary>O token completo, como o Mestre o vê.</summary>
    public static TokenDto De(Token token) => Para(token, VisaoDoToken.Mestre)!;

    /// <summary>O token para a visão dada, ou null se ela não pode saber que ele existe.</summary>
    public static TokenDto? Para(Token token, VisaoDoToken visao)
    {
        var veTudo = visao != VisaoDoToken.Jogador;
        if (!veTudo && !token.Visivel)
        {
            return null;
        }

        var veNumeros = veTudo || token.ExibicaoHp == ExibicaoHp.Completo;
        var veBarra = veNumeros || token.ExibicaoHp == ExibicaoHp.Barra;
        return new TokenDto(
            token.Id,
            token.MapaId,
            token.Tipo,
            token.Nome,
            veNumeros ? token.HpAtual : null,
            veNumeros ? token.HpMaximo : null,
            token.X,
            token.Y,
            token.CorBorda,
            token.Icone,
            token.PersonagemId,
            token.DonoUsuarioId,
            token.Visivel,
            token.Versao,
            token.ImagemId,
            veBarra ? token.HpPercentual : null,
            token.Caido,
            token.ExibicaoHp,
            veTudo ? token.Deslocamento : null,
            veTudo ? token.MovimentoRestante : null,
            veTudo ? token.MovimentoPendente : null,
            token.Visao,
            veTudo ? token.Disparada : null);
    }

    public static VisaoDoToken VisaoDe(Token token, Guid usuarioId, PapelNaMesa papel) =>
        papel == PapelNaMesa.Mestre ? VisaoDoToken.Mestre
        : token.DonoUsuarioId == usuarioId ? VisaoDoToken.Dono
        : VisaoDoToken.Jogador;
}

/// <summary>O HP de um token como um destinatário o vê, para o evento HpAlterado.</summary>
public record HpDto(Guid TokenId, int? HpAtual, int? HpMaximo, int? HpPercentual, bool Caido, int Versao)
{
    public static HpDto De(TokenDto token) =>
        new(token.Id, token.HpAtual, token.HpMaximo, token.HpPercentual, token.Caido, token.Versao);

    /// <summary>true se os dois mostram o mesmo HP, sem contar a versão.</summary>
    public bool MostraOMesmoQue(HpDto outro) => this with { Versao = 0 } == outro with { Versao = 0 };
}

/// <summary>O movimento por blocos de um token, para o evento MovimentoAlterado (só Mestre e dono).</summary>
public record MovimentoDto(Guid TokenId, int Deslocamento, int MovimentoRestante, bool MovimentoPendente, int Versao, bool Disparada)
{
    public static MovimentoDto De(TokenDto token) =>
        new(token.Id, token.Deslocamento!.Value, token.MovimentoRestante!.Value, token.MovimentoPendente!.Value, token.Versao, token.Disparada!.Value);
}

/// <summary>
/// O mesmo token montado para cada público. Os avisos em tempo real partem daqui: quem envia
/// escolhe a projeção pelo destinatário e não tem como mandar a errada por engano de filtro.
/// </summary>
/// <param name="Dono">Nulo quando o token não é de um jogador.</param>
/// <param name="Jogadores">Nulo quando o token está oculto: os demais jogadores não recebem nada.</param>
public record ProjecoesDoToken(Guid TokenId, Guid MapaId, Guid? DonoUsuarioId, TokenDto Mestre, TokenDto? Dono, TokenDto? Jogadores)
{
    public static ProjecoesDoToken De(Token token) => new(
        token.Id,
        token.MapaId,
        token.DonoUsuarioId,
        TokenDto.De(token),
        token.DonoUsuarioId is null ? null : TokenDto.Para(token, VisaoDoToken.Dono),
        TokenDto.Para(token, VisaoDoToken.Jogador));
}

/// <param name="Bloqueia">O bloqueio efetivo: porta fechada, ou os demais tipos com BloqueiaMovimento.</param>
public record EstruturaDto(
    Guid Id,
    Guid MapaId,
    TipoEstrutura Tipo,
    int X,
    int Y,
    int Largura,
    int Profundidade,
    double Altura,
    int RotacaoGraus,
    string Cor,
    Guid? TexturaImagemId,
    string? TexturaPadrao,
    bool BloqueiaMovimento,
    bool PortaAberta,
    bool Bloqueia,
    int Versao)
{
    public static EstruturaDto De(Estrutura estrutura) => new(
        estrutura.Id,
        estrutura.MapaId,
        estrutura.Tipo,
        estrutura.X,
        estrutura.Y,
        estrutura.Largura,
        estrutura.Profundidade,
        estrutura.Altura,
        estrutura.RotacaoGraus,
        estrutura.Cor,
        estrutura.TexturaImagemId,
        estrutura.TexturaPadrao,
        estrutura.BloqueiaMovimento,
        estrutura.PortaAberta,
        estrutura.Bloqueia,
        estrutura.Versao);
}

/// <summary>Estado completo de um mapa, devolvido ao entrar na mesa, ao reconectar e ao trocar o mapa ativo.</summary>
/// <param name="Memorias">As camadas da névoa que o usuário pode ver (todas, para o Mestre).</param>
public record MapaEstadoDto(
    MapaDto Mapa, IReadOnlyList<TokenDto> Tokens, IReadOnlyList<EstruturaDto> Estruturas, IReadOnlyList<MemoriaDeNevoaDto> Memorias);

/// <summary>
/// Resposta do MoverToken (e dos demais métodos do movimento por blocos) a quem pediu. Na rejeição, <paramref name="Token"/> traz o estado atual
/// para o cliente se corrigir (ausente quando o pedido nem chegou a ser avaliado), montado para a
/// visão de quem pediu.
/// </summary>
public record MovimentoResultado(bool Aceito, string? Motivo, TokenDto? Token)
{
    public static MovimentoResultado Ok(Token token, VisaoDoToken visao) => new(true, null, TokenDto.Para(token, visao));

    public static MovimentoResultado Rejeitado(string motivo, Token token, VisaoDoToken visao) =>
        new(false, motivo, TokenDto.Para(token, visao));

    public static MovimentoResultado Rejeitado(string motivo) => new(false, motivo, null);
}
