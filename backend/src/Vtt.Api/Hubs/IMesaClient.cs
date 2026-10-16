using Vtt.Application.Mapas;
using Vtt.Application.Mesas;
using Vtt.Domain.Mesas;

namespace Vtt.Api.Hubs;

/// <summary>
/// Eventos que o servidor envia aos clientes conectados a uma mesa: lobby e mapa.
/// Os eventos de token chegam a cada destinatário já filtrados pelo que ele pode ver.
/// </summary>
public interface IMesaClient
{
    Task MembroEntrou(Guid mesaId, MembroDto membro);

    Task MembroSaiu(Guid mesaId, Guid usuarioId);

    Task MembroOnline(Guid mesaId, Guid usuarioId);

    Task MembroOffline(Guid mesaId, Guid usuarioId);

    Task PersonagemAtivoAlterado(Guid mesaId, Guid usuarioId, PersonagemResumoDto? personagem);

    Task SessaoAlterada(Guid mesaId, bool ativa);

    Task MovimentacaoAlterada(Guid mesaId, ModoDeMovimentacao modo);

    Task MesaExcluida(Guid mesaId);

    /// <summary>Enviado só ao jogador que o Mestre removeu, para o cliente tirá-lo do lobby.</summary>
    Task RemovidoDaMesa(Guid mesaId);

    Task TokenCriado(Guid mesaId, TokenDto token);

    Task TokenMovido(Guid mesaId, Guid mapaId, Guid tokenId, int x, int y, int versao);

    Task TokenAtualizado(Guid mesaId, TokenDto token);

    /// <summary>Movimento restante ou pendência de um token. Só o Mestre e o dono recebem.</summary>
    Task MovimentoAlterado(Guid mesaId, Guid mapaId, MovimentoDto movimento);

    Task TokenRemovido(Guid mesaId, Guid mapaId, Guid tokenId);

    /// <summary>O HP de um token mudou. Os campos vêm conforme o que o destinatário pode ver.</summary>
    Task HpAlterado(Guid mesaId, Guid mapaId, HpDto hp);

    /// <summary>Traz o estado completo do novo mapa ativo, ou null se a mesa ficou sem mapa ativo.</summary>
    Task MapaAtivoAlterado(Guid mesaId, MapaEstadoDto? estado);

    Task MapaAtualizado(Guid mesaId, MapaDto mapa);

    Task EstruturaAdicionada(Guid mesaId, EstruturaDto estrutura);

    Task EstruturaAtualizada(Guid mesaId, EstruturaDto estrutura);

    Task EstruturaRemovida(Guid mesaId, Guid mapaId, Guid estruturaId);

    /// <summary>Uma camada da névoa, inteira. A memória individual de um jogador só chega a ele e ao Mestre.</summary>
    Task MemoriaDeNevoaAlterada(Guid mesaId, MemoriaDeNevoaDto memoria);
}

/// <summary>Estado completo da mesa, devolvido ao entrar ou reconectar: lobby e mapa ativo (se houver).</summary>
public record LobbyDto(MesaDetalheDto Mesa, IReadOnlyCollection<Guid> UsuariosOnline, MapaEstadoDto? MapaAtivo);
