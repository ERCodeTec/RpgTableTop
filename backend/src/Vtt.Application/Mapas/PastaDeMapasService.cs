using Microsoft.EntityFrameworkCore;
using Vtt.Application.Common;
using Vtt.Application.Mesas;
using Vtt.Domain.Mapas;

namespace Vtt.Application.Mapas;

public class PastaDeMapasService(IAppDbContext db, TimeProvider relogio) : IPastaDeMapasService
{
    private const string NomeRepetido = "Já existe uma pasta com este nome nesta mesa.";

    public async Task<Resultado<IReadOnlyList<PastaDeMapasDto>>> ListarAsync(Guid usuarioId, Guid mesaId, CancellationToken cancellationToken = default)
    {
        var erro = await db.ExigirMestreAsync(usuarioId, mesaId, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        var pastas = await db.PastasDeMapas
            .AsNoTracking()
            .Where(p => p.MesaId == mesaId)
            .Select(p => new PastaDeMapasDto(p.Id, p.Nome, db.Mapas.Count(m => m.PastaId == p.Id), p.Cor))
            .ToListAsync(cancellationToken);

        // Ordem alfabética do português, sem depender da collation do banco.
        var ordenadas = pastas.OrderBy(p => p.Nome, StringComparer.Create(new System.Globalization.CultureInfo("pt-BR"), true)).ToList();
        // Conversão explícita: o operador implícito não aceita interface.
        return new Resultado<IReadOnlyList<PastaDeMapasDto>>(ordenadas, null);
    }

    public async Task<Resultado<PastaDeMapasDto>> CriarAsync(Guid usuarioId, Guid mesaId, PastaDeMapasRequest request, CancellationToken cancellationToken = default)
    {
        var erro = await db.ExigirMestreAsync(usuarioId, mesaId, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        var nome = request.Nome!.Trim();
        if (await db.PastasDeMapas.CountAsync(p => p.MesaId == mesaId, cancellationToken) >= PastaDeMapas.LimitePorMesa)
        {
            return Erro.Conflito($"Esta mesa já tem {PastaDeMapas.LimitePorMesa} pastas, que é o limite. Exclua alguma para criar outra.");
        }

        if (await NomeEmUsoAsync(mesaId, nome, ignorarId: null, cancellationToken))
        {
            return Erro.Conflito(NomeRepetido);
        }

        var pasta = new PastaDeMapas { Id = Guid.NewGuid(), MesaId = mesaId, Nome = nome, Cor = NormalizarCor(request.Cor), CriadaEm = relogio.GetUtcNow() };
        db.PastasDeMapas.Add(pasta);
        if (!await SalvarAsync(cancellationToken))
        {
            return Erro.Conflito(NomeRepetido);
        }

        return new PastaDeMapasDto(pasta.Id, pasta.Nome, 0, pasta.Cor);
    }

    public async Task<Resultado<PastaDeMapasDto>> RenomearAsync(Guid usuarioId, Guid id, PastaDeMapasRequest request, CancellationToken cancellationToken = default)
    {
        var (pasta, erro) = await ObterComoMestreAsync(usuarioId, id, cancellationToken);
        if (pasta is null)
        {
            return erro!;
        }

        var nome = request.Nome!.Trim();
        if (await NomeEmUsoAsync(pasta.MesaId, nome, ignorarId: id, cancellationToken))
        {
            return Erro.Conflito(NomeRepetido);
        }

        pasta.Nome = nome;
        pasta.Cor = NormalizarCor(request.Cor);
        if (!await SalvarAsync(cancellationToken))
        {
            return Erro.Conflito(NomeRepetido);
        }

        var quantidade = await db.Mapas.CountAsync(m => m.PastaId == id, cancellationToken);
        return new PastaDeMapasDto(pasta.Id, pasta.Nome, quantidade, pasta.Cor);
    }

    public async Task<Resultado> ExcluirAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken = default)
    {
        var (pasta, erro) = await ObterComoMestreAsync(usuarioId, id, cancellationToken);
        if (pasta is null)
        {
            return erro!;
        }

        // A chave estrangeira (ON DELETE SET NULL) deixa os mapas sem pasta.
        db.PastasDeMapas.Remove(pasta);
        await db.SaveChangesAsync(cancellationToken);
        return Resultado.Ok;
    }

    public async Task<Resultado<MapaDto>> DefinirPastaDoMapaAsync(Guid usuarioId, Guid mapaId, PastaDoMapaRequest request, CancellationToken cancellationToken = default)
    {
        var mapa = await db.Mapas.FirstOrDefaultAsync(m => m.Id == mapaId, cancellationToken);
        if (mapa is null)
        {
            return Erro.NaoEncontrado();
        }

        var erro = await db.ExigirMestreAsync(usuarioId, mapa.MesaId, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        if (request.PastaId is { } pastaId
            && !await db.PastasDeMapas.AnyAsync(p => p.Id == pastaId && p.MesaId == mapa.MesaId, cancellationToken))
        {
            return Erro.Invalido("pastaId", "Escolha uma das pastas desta mesa.");
        }

        // Só organiza a lista do Mestre: não há evento em tempo real para os jogadores.
        mapa.PastaId = request.PastaId;
        await db.SaveChangesAsync(cancellationToken);
        return MapaDto.De(mapa);
    }

    private async Task<(PastaDeMapas? Pasta, Erro? Erro)> ObterComoMestreAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken)
    {
        var pasta = await db.PastasDeMapas.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (pasta is null)
        {
            return (null, Erro.NaoEncontrado());
        }

        var erro = await db.ExigirMestreAsync(usuarioId, pasta.MesaId, cancellationToken);
        return erro is null ? (pasta, null) : (null, erro);
    }

    // O request já passou pelo PastaDeMapasRequestValidator; aqui só normalizamos.
    private static string NormalizarCor(string? cor) =>
        string.IsNullOrEmpty(cor) ? PastaDeMapas.CorPadrao : cor.ToUpperInvariant();

    private Task<bool> NomeEmUsoAsync(Guid mesaId, string nome, Guid? ignorarId, CancellationToken cancellationToken)
    {
        var minusculo = nome.ToLower();
        return db.PastasDeMapas.AnyAsync(
            p => p.MesaId == mesaId && p.Id != ignorarId && p.Nome.ToLower() == minusculo, cancellationToken);
    }

    /// <returns>false se o índice único de nome barrou a gravação.</returns>
    private async Task<bool> SalvarAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }
}
