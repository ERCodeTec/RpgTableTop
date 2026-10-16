namespace Vtt.Api.Hubs;

/// <summary>
/// Quem está online em cada mesa, contando conexões: o mesmo usuário em duas abas só fica
/// offline quando a última conexão cai.
/// Limitação: o estado vive na memória deste processo. Reiniciar a API zera a presença (os
/// clientes reconectam e ela se refaz) e, com mais de uma instância, cada uma enxergaria só
/// as próprias conexões — nesse caso seria preciso um backplane (ex.: Redis).
/// </summary>
public class PresencaTracker
{
    private readonly Lock _trava = new();
    private readonly Dictionary<Guid, Dictionary<Guid, HashSet<string>>> _conexoesPorMesa = [];
    private readonly Dictionary<string, (Guid UsuarioId, HashSet<Guid> Mesas)> _mesasPorConexao = [];
    private readonly Dictionary<Guid, HashSet<Guid>> _mestresPorMesa = [];

    /// <returns>true se o usuário passou a estar online na mesa (é a primeira conexão dele nela).</returns>
    public bool Entrar(Guid mesaId, Guid usuarioId, string connectionId, bool ehMestre = false)
    {
        lock (_trava)
        {
            if (ehMestre)
            {
                if (!_mestresPorMesa.TryGetValue(mesaId, out var mestres))
                {
                    _mestresPorMesa[mesaId] = mestres = [];
                }

                mestres.Add(usuarioId);
            }

            if (!_conexoesPorMesa.TryGetValue(mesaId, out var usuarios))
            {
                _conexoesPorMesa[mesaId] = usuarios = [];
            }

            if (!usuarios.TryGetValue(usuarioId, out var conexoes))
            {
                usuarios[usuarioId] = conexoes = [];
            }

            var ficouOnline = conexoes.Count == 0;
            conexoes.Add(connectionId);

            if (!_mesasPorConexao.TryGetValue(connectionId, out var registro))
            {
                _mesasPorConexao[connectionId] = registro = (usuarioId, []);
            }

            registro.Mesas.Add(mesaId);
            return ficouOnline;
        }
    }

    /// <returns>true se o usuário ficou offline na mesa (era a última conexão dele nela).</returns>
    public bool Sair(Guid mesaId, string connectionId)
    {
        lock (_trava)
        {
            if (!_mesasPorConexao.TryGetValue(connectionId, out var registro) || !registro.Mesas.Remove(mesaId))
            {
                return false;
            }

            if (registro.Mesas.Count == 0)
            {
                _mesasPorConexao.Remove(connectionId);
            }

            return RemoverConexao(mesaId, registro.UsuarioId, connectionId);
        }
    }

    /// <returns>As mesas em que o usuário ficou offline com a queda desta conexão.</returns>
    public (Guid UsuarioId, IReadOnlyList<Guid> MesasOffline) Desconectar(string connectionId)
    {
        lock (_trava)
        {
            if (!_mesasPorConexao.Remove(connectionId, out var registro))
            {
                return (Guid.Empty, []);
            }

            var offline = registro.Mesas
                .Where(mesaId => RemoverConexao(mesaId, registro.UsuarioId, connectionId))
                .ToList();
            return (registro.UsuarioId, offline);
        }
    }

    /// <summary>Tira o usuário da presença da mesa (saiu ou foi removido).</summary>
    /// <returns>As conexões dele que estavam no lobby, para serem tiradas do grupo do SignalR.</returns>
    public IReadOnlyList<string> RemoverUsuario(Guid mesaId, Guid usuarioId)
    {
        lock (_trava)
        {
            if (!_conexoesPorMesa.TryGetValue(mesaId, out var usuarios) || !usuarios.Remove(usuarioId, out var conexoes))
            {
                return [];
            }

            if (usuarios.Count == 0)
            {
                _conexoesPorMesa.Remove(mesaId);
            }

            foreach (var connectionId in conexoes)
            {
                EsquecerMesaDaConexao(connectionId, mesaId);
            }

            return [.. conexoes];
        }
    }

    /// <returns>Todas as conexões que estavam no lobby da mesa excluída.</returns>
    public IReadOnlyList<string> RemoverMesa(Guid mesaId)
    {
        lock (_trava)
        {
            _mestresPorMesa.Remove(mesaId);
            if (!_conexoesPorMesa.Remove(mesaId, out var usuarios))
            {
                return [];
            }

            var conexoes = usuarios.Values.SelectMany(c => c).ToList();
            foreach (var connectionId in conexoes)
            {
                EsquecerMesaDaConexao(connectionId, mesaId);
            }

            return conexoes;
        }
    }

    /// <summary>Quem, entre os que já acompanharam a mesa neste processo, é Mestre dela.</summary>
    public IReadOnlySet<Guid> MestresOnline(Guid mesaId)
    {
        lock (_trava)
        {
            return _mestresPorMesa.TryGetValue(mesaId, out var mestres) ? new HashSet<Guid>(mestres) : new HashSet<Guid>();
        }
    }

    /// <summary>As conexões do usuário que estão acompanhando a mesa.</summary>
    public IReadOnlyList<string> ConexoesDoUsuario(Guid mesaId, Guid usuarioId)
    {
        lock (_trava)
        {
            return _conexoesPorMesa.TryGetValue(mesaId, out var usuarios) && usuarios.TryGetValue(usuarioId, out var conexoes)
                ? [.. conexoes]
                : [];
        }
    }

    public IReadOnlyCollection<Guid> UsuariosOnline(Guid mesaId)
    {
        lock (_trava)
        {
            return _conexoesPorMesa.TryGetValue(mesaId, out var usuarios) ? [.. usuarios.Keys] : [];
        }
    }

    private bool RemoverConexao(Guid mesaId, Guid usuarioId, string connectionId)
    {
        if (!_conexoesPorMesa.TryGetValue(mesaId, out var usuarios)
            || !usuarios.TryGetValue(usuarioId, out var conexoes)
            || !conexoes.Remove(connectionId)
            || conexoes.Count > 0)
        {
            return false;
        }

        usuarios.Remove(usuarioId);
        if (usuarios.Count == 0)
        {
            _conexoesPorMesa.Remove(mesaId);
        }

        return true;
    }

    private void EsquecerMesaDaConexao(string connectionId, Guid mesaId)
    {
        if (_mesasPorConexao.TryGetValue(connectionId, out var registro)
            && registro.Mesas.Remove(mesaId)
            && registro.Mesas.Count == 0)
        {
            _mesasPorConexao.Remove(connectionId);
        }
    }
}
