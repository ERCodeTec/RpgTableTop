using Vtt.Domain.Personagens;

namespace Vtt.Domain.Mesas;

public class MesaMembro
{
    public Guid Id { get; set; }
    public Guid MesaId { get; set; }
    public Guid UsuarioId { get; set; }
    public PapelNaMesa Papel { get; set; }
    public Guid? PersonagemAtivoId { get; set; }
    public DateTimeOffset EntrouEm { get; set; }

    public Personagem? PersonagemAtivo { get; set; }
}
