using Microsoft.AspNetCore.SignalR;
using Vtt.Application.Mapas;

namespace Vtt.Api.Hubs;

/// <summary>
/// Entrega os eventos do mapa pelo mesmo Hub do lobby. Como no <see cref="MesaNotifier"/>, uma
/// falha de envio é só registrada: a alteração já foi gravada e o cliente recupera o estado
/// completo ao reconectar.
/// Nos eventos de token há três públicos, cada um com a própria projeção: o grupo do Mestre, as
/// conexões do dono e o grupo dos jogadores (menos o dono). O grupo por papel garante que um
/// payload de Mestre nunca seja endereçado a um jogador; a projeção, que o payload de jogador
/// nunca traga o que ele não pode ver.
/// </summary>
public class MapaNotifier(
    IHubContext<MesaHub, IMesaClient> hub,
    PresencaTracker presenca,
    ILogger<MapaNotifier> logger) : IMapaNotifier
{
    public Task TokenCriadoAsync(AlcanceDoMapa alcance, ProjecoesDoToken token) =>
        EnviarPorPublicoAsync(alcance, token.DonoUsuarioId, nameof(IMesaClient.TokenCriado),
            aoMestre: destino => destino.TokenCriado(alcance.MesaId, token.Mestre),
            aoDono: token.Dono is null ? null : destino => destino.TokenCriado(alcance.MesaId, token.Dono),
            aosJogadores: token.Jogadores is null ? null : destino => destino.TokenCriado(alcance.MesaId, token.Jogadores));

    public Task TokenAlteradoAsync(AlcanceDoMapa alcance, ProjecoesDoToken antes, ProjecoesDoToken depois) =>
        EnviarPorPublicoAsync(alcance, depois.DonoUsuarioId, nameof(IMesaClient.TokenAtualizado),
            aoMestre: destino => destino.TokenAtualizado(alcance.MesaId, depois.Mestre),
            aoDono: depois.Dono is null ? null : destino => destino.TokenAtualizado(alcance.MesaId, depois.Dono),
            // Para quem não via o token e passou a ver, ele acaba de surgir; para quem deixou de
            // ver, ele some, sem nada que diga o motivo.
            aosJogadores: (antes.Jogadores, depois.Jogadores) switch
            {
                (null, null) => null,
                (null, { } revelado) => destino => destino.TokenCriado(alcance.MesaId, revelado),
                ({ }, null) => destino => destino.TokenRemovido(alcance.MesaId, depois.MapaId, depois.TokenId),
                (_, { } atualizado) => destino => destino.TokenAtualizado(alcance.MesaId, atualizado)
            });

    public Task TokenMovidoAsync(AlcanceDoMapa alcance, ProjecoesDoToken token)
    {
        var (x, y, versao) = (token.Mestre.X, token.Mestre.Y, token.Mestre.Versao);
        Task Mover(IMesaClient destino) => destino.TokenMovido(alcance.MesaId, token.MapaId, token.TokenId, x, y, versao);
        return EnviarPorPublicoAsync(alcance, token.DonoUsuarioId, nameof(IMesaClient.TokenMovido),
            aoMestre: Mover,
            aoDono: token.Dono is null ? null : Mover,
            aosJogadores: token.Jogadores is null ? null : Mover);
    }

    public Task MovimentoAlteradoAsync(AlcanceDoMapa alcance, ProjecoesDoToken token) =>
        EnviarPorPublicoAsync(alcance, token.DonoUsuarioId, nameof(IMesaClient.MovimentoAlterado),
            aoMestre: destino => destino.MovimentoAlterado(alcance.MesaId, token.MapaId, MovimentoDto.De(token.Mestre)),
            aoDono: token.Dono is null ? null : destino => destino.MovimentoAlterado(alcance.MesaId, token.MapaId, MovimentoDto.De(token.Dono)),
            aosJogadores: null);

    public Task TokenRemovidoAsync(AlcanceDoMapa alcance, ProjecoesDoToken token)
    {
        Task Remover(IMesaClient destino) => destino.TokenRemovido(alcance.MesaId, token.MapaId, token.TokenId);
        return EnviarPorPublicoAsync(alcance, token.DonoUsuarioId, nameof(IMesaClient.TokenRemovido),
            aoMestre: Remover,
            aoDono: token.Dono is null ? null : Remover,
            aosJogadores: token.Jogadores is null ? null : Remover);
    }

    public Task HpAlteradoAsync(AlcanceDoMapa alcance, ProjecoesDoToken antes, ProjecoesDoToken depois)
    {
        // O jogador só é avisado se o HP que ele vê mudou: dano em um token de HP oculto não gera
        // nada para ele, e em um token só com barra, apenas quando a faixa muda.
        var paraJogadores = depois.Jogadores is null ? null : HpDto.De(depois.Jogadores);
        var mudouParaJogadores = paraJogadores is not null
            && (antes.Jogadores is null || !paraJogadores.MostraOMesmoQue(HpDto.De(antes.Jogadores)));

        return EnviarPorPublicoAsync(alcance, depois.DonoUsuarioId, nameof(IMesaClient.HpAlterado),
            aoMestre: destino => destino.HpAlterado(alcance.MesaId, depois.MapaId, HpDto.De(depois.Mestre)),
            aoDono: depois.Dono is null ? null : destino => destino.HpAlterado(alcance.MesaId, depois.MapaId, HpDto.De(depois.Dono)),
            aosJogadores: mudouParaJogadores ? destino => destino.HpAlterado(alcance.MesaId, depois.MapaId, paraJogadores!) : null);
    }

    public async Task MapaAtivoAlteradoAsync(Guid mesaId, Func<Guid, bool, MapaEstadoDto>? estadoPara)
    {
        try
        {
            if (estadoPara is null)
            {
                await hub.Clients.Group(MesaHub.Grupo(mesaId)).MapaAtivoAlterado(mesaId, null);
                return;
            }

            // O estado traz tokens, então cada pessoa conectada recebe o dela.
            var mestres = presenca.MestresOnline(mesaId);
            foreach (var usuarioId in presenca.UsuariosOnline(mesaId))
            {
                var conexoes = presenca.ConexoesDoUsuario(mesaId, usuarioId);
                if (conexoes.Count > 0)
                {
                    await hub.Clients.Clients(conexoes).MapaAtivoAlterado(mesaId, estadoPara(usuarioId, mestres.Contains(usuarioId)));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao enviar {Evento} para a mesa {MesaId}", nameof(IMesaClient.MapaAtivoAlterado), mesaId);
        }
    }

    public Task MapaAtualizadoAsync(AlcanceDoMapa alcance, MapaDto mapa) =>
        EnviarATodosAsync(alcance, nameof(IMesaClient.MapaAtualizado), destino => destino.MapaAtualizado(alcance.MesaId, mapa));

    public Task EstruturaAdicionadaAsync(AlcanceDoMapa alcance, EstruturaDto estrutura) =>
        EnviarATodosAsync(alcance, nameof(IMesaClient.EstruturaAdicionada), destino => destino.EstruturaAdicionada(alcance.MesaId, estrutura));

    public Task EstruturaAtualizadaAsync(AlcanceDoMapa alcance, EstruturaDto estrutura) =>
        EnviarATodosAsync(alcance, nameof(IMesaClient.EstruturaAtualizada), destino => destino.EstruturaAtualizada(alcance.MesaId, estrutura));

    public Task EstruturaRemovidaAsync(AlcanceDoMapa alcance, Guid mapaId, Guid estruturaId) =>
        EnviarATodosAsync(alcance, nameof(IMesaClient.EstruturaRemovida),
            destino => destino.EstruturaRemovida(alcance.MesaId, mapaId, estruturaId));

    public async Task MemoriasDeNevoaAlteradasAsync(AlcanceDoMapa alcance, IReadOnlyList<MemoriaDeNevoaDto> memorias)
    {
        foreach (var memoria in memorias)
        {
            if (memoria.UsuarioId is not { } dono)
            {
                await EnviarATodosAsync(alcance, nameof(IMesaClient.MemoriaDeNevoaAlterada),
                    destino => destino.MemoriaDeNevoaAlterada(alcance.MesaId, memoria));
                continue;
            }

            await EnviarPorPublicoAsync(alcance, dono, nameof(IMesaClient.MemoriaDeNevoaAlterada),
                aoMestre: destino => destino.MemoriaDeNevoaAlterada(alcance.MesaId, memoria),
                aoDono: destino => destino.MemoriaDeNevoaAlterada(alcance.MesaId, memoria),
                aosJogadores: null);
        }
    }

    /// <summary>Eventos sem dados de token: iguais para a mesa inteira (ou só para o Mestre, em mapa em preparação).</summary>
    private async Task EnviarATodosAsync(AlcanceDoMapa alcance, string evento, Func<IMesaClient, Task> enviar)
    {
        try
        {
            await enviar(alcance.SomenteUsuarioId is null
                ? hub.Clients.Group(MesaHub.Grupo(alcance.MesaId))
                : hub.Clients.Group(MesaHub.GrupoDoMestre(alcance.MesaId)));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao enviar {Evento} para a mesa {MesaId}", evento, alcance.MesaId);
        }
    }

    /// <summary>
    /// Envia a cada público a sua versão do evento. Um envio nulo significa que aquele público não
    /// recebe nada. Em mapa em preparação, só o Mestre é avisado.
    /// </summary>
    private async Task EnviarPorPublicoAsync(
        AlcanceDoMapa alcance,
        Guid? donoUsuarioId,
        string evento,
        Func<IMesaClient, Task> aoMestre,
        Func<IMesaClient, Task>? aoDono,
        Func<IMesaClient, Task>? aosJogadores)
    {
        try
        {
            await aoMestre(hub.Clients.Group(MesaHub.GrupoDoMestre(alcance.MesaId)));
            if (alcance.SomenteUsuarioId is not null)
            {
                return;
            }

            var conexoesDoDono = donoUsuarioId is { } dono ? presenca.ConexoesDoUsuario(alcance.MesaId, dono) : [];
            if (aoDono is not null && conexoesDoDono.Count > 0)
            {
                await aoDono(hub.Clients.Clients(conexoesDoDono));
            }

            if (aosJogadores is not null)
            {
                await aosJogadores(hub.Clients.GroupExcept(MesaHub.GrupoDosJogadores(alcance.MesaId), conexoesDoDono));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao enviar {Evento} para a mesa {MesaId}", evento, alcance.MesaId);
        }
    }
}
