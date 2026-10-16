using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Vtt.Api.Infrastructure;
using Vtt.Application.Common;
using Vtt.Application.Mapas;
using Vtt.Application.Mesas;
using Vtt.Domain.Mesas;

namespace Vtt.Api.Hubs;

/// <summary>
/// Lobby e mapa em tempo real. O Hub cuida de quem escuta cada mesa, da presença e do movimento
/// dos tokens; as demais mudanças de estado passam pelos endpoints REST, que avisam o grupo via
/// <see cref="MesaNotifier"/> e <see cref="MapaNotifier"/>.
/// </summary>
[Authorize]
public class MesaHub(
    IMesaService mesas,
    IMapaService mapas,
    ITokenService tokens,
    INevoaService nevoa,
    PresencaTracker presenca,
    LimitadorDoHub limitador,
    IOptions<RateLimitingOptions> limites,
    ILogger<MesaHub> logger) : Hub<IMesaClient>
{
    public const string Caminho = "/hubs/mesa";

    /// <summary>Todos os que acompanham a mesa: eventos do lobby e do cenário, iguais para todos.</summary>
    public static string Grupo(Guid mesaId) => $"mesa:{mesaId}";

    /// <summary>Só o Mestre: recebe os tokens completos e os eventos de mapas em preparação.</summary>
    public static string GrupoDoMestre(Guid mesaId) => $"mesa:{mesaId}:mestre";

    /// <summary>Só os jogadores: recebem os tokens já filtrados (sem ocultos, HP conforme a exibição).</summary>
    public static string GrupoDosJogadores(Guid mesaId) => $"mesa:{mesaId}:jogadores";

    /// <summary>Tira a conexão de todos os grupos da mesa.</summary>
    public static async Task SairDosGruposAsync(IGroupManager grupos, string connectionId, Guid mesaId)
    {
        await grupos.RemoveFromGroupAsync(connectionId, Grupo(mesaId));
        await grupos.RemoveFromGroupAsync(connectionId, GrupoDoMestre(mesaId));
        await grupos.RemoveFromGroupAsync(connectionId, GrupoDosJogadores(mesaId));
    }

    /// <summary>
    /// Passa a receber os eventos da mesa e devolve o estado atual do lobby e do mapa ativo, como
    /// quem chama pode vê-lo. É também o que o cliente chama ao reconectar, para trocar o estado
    /// local inteiro pelo do servidor.
    /// </summary>
    public async Task<LobbyDto> EntrarNaMesa(Guid mesaId)
    {
        ExigirDentroDoLimite(nameof(EntrarNaMesa), limites.Value.ChamadasDoHubPorSegundo);

        var usuarioId = Context.User!.ObterUsuarioId();
        var resultado = await mesas.ObterAsync(usuarioId, mesaId, Context.ConnectionAborted);
        if (!resultado.Sucesso)
        {
            logger.LogWarning("Usuário {UsuarioId} tentou entrar na mesa {MesaId} sem ser membro", usuarioId, mesaId);
            throw new HubException("Mesa não encontrada.");
        }

        // Entra nos grupos antes de ler o estado do mapa: um evento que chegue nesse intervalo é
        // reaplicado pelo cliente sobre o estado devolvido, em vez de se perder.
        var papel = resultado.Valor!.MeuPapel;
        var ehMestre = papel == PapelNaMesa.Mestre;
        await Groups.AddToGroupAsync(Context.ConnectionId, Grupo(mesaId), Context.ConnectionAborted);
        await Groups.AddToGroupAsync(
            Context.ConnectionId, ehMestre ? GrupoDoMestre(mesaId) : GrupoDosJogadores(mesaId), Context.ConnectionAborted);

        if (presenca.Entrar(mesaId, usuarioId, Context.ConnectionId, ehMestre))
        {
            await Clients.OthersInGroup(Grupo(mesaId)).MembroOnline(mesaId, usuarioId);
        }

        // O estado do lobby é relido depois de entrar no grupo, pelo mesmo motivo.
        var atual = await mesas.ObterAsync(usuarioId, mesaId, Context.ConnectionAborted);
        var mapaAtivo = await mapas.ObterEstadoAtivoAsync(mesaId, usuarioId, papel, Context.ConnectionAborted);
        return new LobbyDto(atual.Valor ?? resultado.Valor, presenca.UsuariosOnline(mesaId), mapaAtivo);
    }

    /// <summary>Deixa de acompanhar a mesa sem fechar a conexão (o jogador saiu da tela do lobby).</summary>
    public async Task SairDoLobby(Guid mesaId)
    {
        ExigirDentroDoLimite(nameof(SairDoLobby), limites.Value.ChamadasDoHubPorSegundo);

        await SairDosGruposAsync(Groups, Context.ConnectionId, mesaId);
        if (presenca.Sair(mesaId, Context.ConnectionId))
        {
            await Clients.Group(Grupo(mesaId)).MembroOffline(mesaId, Context.User!.ObterUsuarioId());
        }
    }

    /// <summary>
    /// Arraste livre do Mestre para a célula (x, y). A resposta vai só para quem pediu; se o
    /// movimento for aceito, quem vê o token recebe TokenMovido. Um movimento rejeitado traz o
    /// estado atual do token.
    /// </summary>
    public Task<MovimentoResultado> MoverToken(Guid tokenId, int x, int y, int versaoEsperada) =>
        MovimentarAsync(nameof(MoverToken), (usuarioId, cancelamento) => tokens.MoverAsync(usuarioId, tokenId, x, y, versaoEsperada, cancelamento));

    /// <summary>
    /// Movimento por blocos, com custo, para o dono ou o Mestre. Além de TokenMovido, o Mestre e o
    /// dono recebem MovimentoAlterado.
    /// </summary>
    public Task<MovimentoResultado> MoverPorBlocos(Guid tokenId, int x, int y, int versaoEsperada) =>
        MovimentarAsync(nameof(MoverPorBlocos), (usuarioId, cancelamento) => tokens.MoverPorBlocosAsync(usuarioId, tokenId, x, y, versaoEsperada, cancelamento));

    public Task<MovimentoResultado> DesfazerMovimento(Guid tokenId, int versaoEsperada) =>
        MovimentarAsync(nameof(DesfazerMovimento), (usuarioId, cancelamento) => tokens.DesfazerMovimentoAsync(usuarioId, tokenId, versaoEsperada, cancelamento));

    public Task<MovimentoResultado> ConcluirMovimento(Guid tokenId, int versaoEsperada) =>
        MovimentarAsync(nameof(ConcluirMovimento), (usuarioId, cancelamento) => tokens.ConcluirMovimentoAsync(usuarioId, tokenId, versaoEsperada, cancelamento));

    /// <summary>Disparada: dobra o movimento do token nesta rodada. O Mestre e o dono recebem MovimentoAlterado.</summary>
    public Task<MovimentoResultado> Disparar(Guid tokenId, int versaoEsperada) =>
        MovimentarAsync(nameof(Disparar), (usuarioId, cancelamento) => tokens.DispararAsync(usuarioId, tokenId, versaoEsperada, cancelamento));

    /// <summary>Desfaz a Disparada enquanto o movimento extra não foi gasto. O Mestre e o dono recebem MovimentoAlterado.</summary>
    public Task<MovimentoResultado> DesfazerDisparada(Guid tokenId, int versaoEsperada) =>
        MovimentarAsync(nameof(DesfazerDisparada), (usuarioId, cancelamento) => tokens.DesfazerDisparadaAsync(usuarioId, tokenId, versaoEsperada, cancelamento));

    /// <summary>Só o Mestre: devolve o movimento de todos os tokens do mapa.</summary>
    public async Task NovaRodada(Guid mapaId)
    {
        ExigirDentroDoLimite(nameof(NovaRodada), limites.Value.ChamadasDoHubPorSegundo);

        var resultado = await tokens.NovaRodadaAsync(Context.User!.ObterUsuarioId(), mapaId, Context.ConnectionAborted);
        if (!resultado.Sucesso)
        {
            throw new HubException(resultado.Erro!.Mensagem);
        }
    }

    /// <summary>Só o Mestre: devolve o movimento de um token.</summary>
    public async Task<TokenDto> RestaurarMovimento(Guid tokenId)
    {
        ExigirDentroDoLimite(nameof(RestaurarMovimento), limites.Value.ChamadasDoHubPorSegundo);

        var resultado = await tokens.RestaurarMovimentoAsync(Context.User!.ObterUsuarioId(), tokenId, Context.ConnectionAborted);
        return resultado.Sucesso ? resultado.Valor! : throw new HubException(resultado.Erro!.Mensagem);
    }

    /// <summary>
    /// Um jogador informa as células que vê agora, no formato de bits da névoa. Chamado a cada
    /// passo, por isso usa o limite dos movimentos; acima dele, a chamada é só ignorada, e o
    /// cliente manda de novo no próximo passo o que ainda faltar.
    /// </summary>
    public async Task ExplorarCelulas(Guid mapaId, byte[] celulas)
    {
        if (!limitador.Permitir(Context.ConnectionId, nameof(ExplorarCelulas), limites.Value.MovimentosPorSegundo))
        {
            return;
        }

        var resultado = await nevoa.ExplorarAsync(Context.User!.ObterUsuarioId(), mapaId, celulas, Context.ConnectionAborted);
        if (!resultado.Sucesso)
        {
            throw new HubException(resultado.Erro!.Mensagem);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        limitador.Esquecer(Context.ConnectionId);
        var (usuarioId, mesasOffline) = presenca.Desconectar(Context.ConnectionId);
        foreach (var mesaId in mesasOffline)
        {
            await Clients.Group(Grupo(mesaId)).MembroOffline(mesaId, usuarioId);
        }

        if (exception is not null)
        {
            logger.LogInformation(exception, "Conexão {ConnectionId} caiu", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Os métodos de movimento: limite próprio por conexão e erro de acesso como HubException.</summary>
    private async Task<MovimentoResultado> MovimentarAsync(
        string metodo, Func<Guid, CancellationToken, Task<Resultado<MovimentoResultado>>> mover)
    {
        if (!limitador.Permitir(Context.ConnectionId, metodo, limites.Value.MovimentosPorSegundo))
        {
            return MovimentoResultado.Rejeitado("Muitos movimentos em sequência. Aguarde um instante.");
        }

        var resultado = await mover(Context.User!.ObterUsuarioId(), Context.ConnectionAborted);
        return resultado.Sucesso ? resultado.Valor! : throw new HubException(resultado.Erro!.Mensagem);
    }

    /// <summary>Proteção contra inundação: cada conexão tem um limite de chamadas por segundo em cada método.</summary>
    private void ExigirDentroDoLimite(string metodo, int porSegundo)
    {
        if (!limitador.Permitir(Context.ConnectionId, metodo, porSegundo))
        {
            throw new HubException("Muitas chamadas em sequência. Aguarde um instante.");
        }
    }
}
