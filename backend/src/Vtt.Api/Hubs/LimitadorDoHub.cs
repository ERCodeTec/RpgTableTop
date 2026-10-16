using System.Collections.Concurrent;

namespace Vtt.Api.Hubs;

/// <summary>
/// Limita as chamadas de cada conexão a cada método do Hub (balde de fichas que se reabastece a
/// cada segundo), para que um cliente não inunde o servidor e a mesa.
/// O estado vive na memória do processo, como a presença.
/// </summary>
public class LimitadorDoHub(TimeProvider relogio)
{
    private readonly ConcurrentDictionary<(string ConnectionId, string Metodo), Balde> _baldes = new();

    public bool Permitir(string connectionId, string metodo, int porSegundo)
    {
        var limite = Math.Max(1, porSegundo);
        var balde = _baldes.GetOrAdd((connectionId, metodo), _ => new Balde { Fichas = limite, Desde = relogio.GetTimestamp() });
        lock (balde)
        {
            var agora = relogio.GetTimestamp();
            balde.Fichas = Math.Min(limite, balde.Fichas + relogio.GetElapsedTime(balde.Desde, agora).TotalSeconds * limite);
            balde.Desde = agora;

            if (balde.Fichas < 1)
            {
                return false;
            }

            balde.Fichas--;
            return true;
        }
    }

    public void Esquecer(string connectionId)
    {
        foreach (var chave in _baldes.Keys.Where(chave => chave.ConnectionId == connectionId).ToList())
        {
            _baldes.TryRemove(chave, out _);
        }
    }

    private sealed class Balde
    {
        public double Fichas;
        public long Desde;
    }
}
