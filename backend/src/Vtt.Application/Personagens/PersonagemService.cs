using Microsoft.EntityFrameworkCore;
using Vtt.Application.Common;
using Vtt.Application.Mapas;
using Vtt.Domain.Personagens;

namespace Vtt.Application.Personagens;

public class PersonagemService(IAppDbContext db, TokensDeJogador tokensDeJogador, TimeProvider relogio) : IPersonagemService
{
    public async Task<IReadOnlyList<PersonagemDto>> ListarAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var personagens = await db.Personagens
            .AsNoTracking()
            .Where(p => p.UsuarioId == usuarioId)
            .OrderBy(p => p.Nome)
            .ThenBy(p => p.CriadoEm)
            .ToListAsync(cancellationToken);

        return personagens.Select(PersonagemDto.De).ToList();
    }

    public async Task<PersonagemDto?> ObterAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken = default)
    {
        var personagem = await db.Personagens
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.UsuarioId == usuarioId, cancellationToken);

        return personagem is null ? null : PersonagemDto.De(personagem);
    }

    public async Task<PersonagemDto> CriarAsync(Guid usuarioId, PersonagemRequest request, CancellationToken cancellationToken = default)
    {
        var agora = relogio.GetUtcNow();
        var personagem = new Personagem
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            CriadoEm = agora,
            AtualizadoEm = agora
        };
        Aplicar(personagem, request);

        db.Personagens.Add(personagem);
        await db.SaveChangesAsync(cancellationToken);

        return PersonagemDto.De(personagem);
    }

    public async Task<PersonagemDto?> AtualizarAsync(Guid usuarioId, Guid id, PersonagemRequest request, CancellationToken cancellationToken = default)
    {
        var personagem = await db.Personagens
            .FirstOrDefaultAsync(p => p.Id == id && p.UsuarioId == usuarioId, cancellationToken);
        if (personagem is null)
        {
            return null;
        }

        Aplicar(personagem, request);
        personagem.AtualizadoEm = relogio.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        await tokensDeJogador.SincronizarPersonagemAsync(personagem, cancellationToken);

        return PersonagemDto.De(personagem);
    }

    public async Task<bool> ExcluirAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken = default)
    {
        var personagem = await db.Personagens
            .FirstOrDefaultAsync(p => p.Id == id && p.UsuarioId == usuarioId, cancellationToken);
        if (personagem is null)
        {
            return false;
        }

        // Os tokens do personagem saem dos mapas junto com ele.
        IReadOnlyList<TokenRemovido> tokensRemovidos;
        await using (var transacao = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            tokensRemovidos = await tokensDeJogador.RemoverDoPersonagemAsync(id, cancellationToken);
            db.Personagens.Remove(personagem);
            await db.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
        }

        await tokensDeJogador.AvisarRemocaoAsync(tokensRemovidos);
        return true;
    }

    // O request já passou pelo PersonagemRequestValidator; aqui só normalizamos.
    private static void Aplicar(Personagem personagem, PersonagemRequest request)
    {
        personagem.Nome = request.Nome!.Trim();
        personagem.Classe = request.Classe!.Value;
        personagem.Nivel = request.Nivel;
        personagem.HpMaximo = request.HpMaximo;
        personagem.Deslocamento = request.Deslocamento;
        personagem.Visao = request.Visao;
        personagem.CorBorda = string.IsNullOrEmpty(request.CorBorda)
            ? Personagem.CorBordaPadrao
            : request.CorBorda.ToUpperInvariant();
    }
}
