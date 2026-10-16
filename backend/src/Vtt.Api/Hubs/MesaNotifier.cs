using Microsoft.AspNetCore.SignalR;
using Vtt.Application.Mesas;

namespace Vtt.Api.Hubs;

/// <summary>
/// Entrega os eventos da mesa pelo SignalR. Uma falha de envio é só registrada: a alteração
/// já foi gravada, e o cliente recupera o estado completo ao reconectar.
/// </summary>
public class MesaNotifier(
    IHubContext<MesaHub, IMesaClient> hub,
    PresencaTracker presenca,
    ILogger<MesaNotifier> logger) : IMesaNotifier
{
    public Task MembroEntrouAsync(Guid mesaId, MembroDto membro) =>
        EnviarAsync(mesaId, nameof(IMesaClient.MembroEntrou), grupo => grupo.MembroEntrou(mesaId, membro));

    public Task MembroSaiuAsync(Guid mesaId, Guid usuarioId, bool removidoPeloMestre) =>
        EnviarAsync(mesaId, nameof(IMesaClient.MembroSaiu), async grupo =>
        {
            // Primeiro tira as conexões dele do grupo, para que não receba mais nada desta mesa.
            foreach (var connectionId in presenca.RemoverUsuario(mesaId, usuarioId))
            {
                await MesaHub.SairDosGruposAsync(hub.Groups, connectionId, mesaId);
            }

            if (removidoPeloMestre)
            {
                await hub.Clients.User(usuarioId.ToString()).RemovidoDaMesa(mesaId);
            }

            await grupo.MembroSaiu(mesaId, usuarioId);
        });

    public Task PersonagemAtivoAlteradoAsync(Guid mesaId, Guid usuarioId, PersonagemResumoDto? personagem) =>
        EnviarAsync(mesaId, nameof(IMesaClient.PersonagemAtivoAlterado),
            grupo => grupo.PersonagemAtivoAlterado(mesaId, usuarioId, personagem));

    public Task SessaoAlteradaAsync(Guid mesaId, bool ativa) =>
        EnviarAsync(mesaId, nameof(IMesaClient.SessaoAlterada), grupo => grupo.SessaoAlterada(mesaId, ativa));

    public Task MesaExcluidaAsync(Guid mesaId) =>
        EnviarAsync(mesaId, nameof(IMesaClient.MesaExcluida), async grupo =>
        {
            await grupo.MesaExcluida(mesaId);
            foreach (var connectionId in presenca.RemoverMesa(mesaId))
            {
                await MesaHub.SairDosGruposAsync(hub.Groups, connectionId, mesaId);
            }
        });

    private async Task EnviarAsync(Guid mesaId, string evento, Func<IMesaClient, Task> enviar)
    {
        try
        {
            await enviar(hub.Clients.Group(MesaHub.Grupo(mesaId)));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao enviar {Evento} para a mesa {MesaId}", evento, mesaId);
        }
    }
}
