using Vtt.Application.Iniciativas;
using Vtt.Domain.Mesas;

namespace Vtt.Application.Mesas;

/// <summary>
/// Avisa em tempo real quem está no lobby da mesa. Os serviços chamam depois de gravar,
/// então as regras de negócio ficam em um só lugar e o Hub apenas transporta.
/// </summary>
public interface IMesaNotifier
{
    Task MembroEntrouAsync(Guid mesaId, MembroDto membro);

    /// <param name="removidoPeloMestre">true avisa também o próprio removido, para o cliente tirá-lo da tela.</param>
    Task MembroSaiuAsync(Guid mesaId, Guid usuarioId, bool removidoPeloMestre);

    Task PersonagemAtivoAlteradoAsync(Guid mesaId, Guid usuarioId, PersonagemResumoDto? personagem);

    Task SessaoAlteradaAsync(Guid mesaId, bool ativa);

    Task MovimentacaoAlteradaAsync(Guid mesaId, ModoDeMovimentacao modo);

    Task MesaExcluidaAsync(Guid mesaId);

    /// <summary>A iniciativa mudou: o Mestre recebe a projeção dele, os jogadores a deles.</summary>
    Task IniciativaAlteradaAsync(Guid mesaId, IniciativaDto mestre, IniciativaDto jogadores);
}
