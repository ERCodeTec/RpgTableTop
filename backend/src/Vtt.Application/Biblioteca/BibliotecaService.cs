using Microsoft.EntityFrameworkCore;
using Vtt.Application.Common;
using Vtt.Domain.Biblioteca;
using Vtt.Domain.Mapas;

namespace Vtt.Application.Biblioteca;

public class BibliotecaService(IAppDbContext db, TimeProvider relogio) : IBibliotecaService
{
    private const string NomeRepetido = "Você já tem uma categoria com este nome.";

    public async Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var categorias = await db.CategoriasDeModelo
            .AsNoTracking()
            .Where(c => c.UsuarioId == usuarioId)
            .Select(c => new CategoriaDto(c.Id, c.Nome, db.ModelosDeToken.Count(m => m.CategoriaId == c.Id)))
            .ToListAsync(cancellationToken);

        // Ordem alfabética do português, sem depender da collation do banco.
        return categorias.OrderBy(c => c.Nome, StringComparer.Create(new System.Globalization.CultureInfo("pt-BR"), true)).ToList();
    }

    public async Task<Resultado<CategoriaDto>> CriarCategoriaAsync(Guid usuarioId, CategoriaRequest request, CancellationToken cancellationToken = default)
    {
        var nome = request.Nome!.Trim();
        if (await db.CategoriasDeModelo.CountAsync(c => c.UsuarioId == usuarioId, cancellationToken) >= CategoriaDeModelo.LimitePorUsuario)
        {
            return Erro.Conflito($"Você já tem {CategoriaDeModelo.LimitePorUsuario} categorias, que é o limite. Exclua alguma para criar outra.");
        }

        if (await NomeEmUsoAsync(usuarioId, nome, ignorarId: null, cancellationToken))
        {
            return Erro.Conflito(NomeRepetido);
        }

        var categoria = new CategoriaDeModelo { Id = Guid.NewGuid(), UsuarioId = usuarioId, Nome = nome, CriadaEm = relogio.GetUtcNow() };
        db.CategoriasDeModelo.Add(categoria);
        if (!await SalvarAsync(cancellationToken))
        {
            return Erro.Conflito(NomeRepetido);
        }

        return new CategoriaDto(categoria.Id, categoria.Nome, 0);
    }

    public async Task<Resultado<IReadOnlyList<CategoriaDto>>> CriarCategoriasSugeridasAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var existentes = await db.CategoriasDeModelo
            .Where(c => c.UsuarioId == usuarioId)
            .Select(c => c.Nome.ToLower())
            .ToListAsync(cancellationToken);

        var faltando = CategoriaDeModelo.Sugeridas.Where(nome => !existentes.Contains(nome.ToLower())).ToList();
        if (existentes.Count + faltando.Count > CategoriaDeModelo.LimitePorUsuario)
        {
            return Erro.Conflito($"Você já tem {existentes.Count} categorias; o limite é {CategoriaDeModelo.LimitePorUsuario}.");
        }

        var agora = relogio.GetUtcNow();
        foreach (var nome in faltando)
        {
            db.CategoriasDeModelo.Add(new CategoriaDeModelo { Id = Guid.NewGuid(), UsuarioId = usuarioId, Nome = nome, CriadaEm = agora });
        }

        // Pedido duplicado ao mesmo tempo: o índice único segura e as que ficaram valem.
        await SalvarAsync(cancellationToken);
        // Conversão explícita: o operador implícito não aceita interface.
        return new Resultado<IReadOnlyList<CategoriaDto>>(await ListarCategoriasAsync(usuarioId, cancellationToken), null);
    }

    public async Task<Resultado<CategoriaDto>> RenomearCategoriaAsync(Guid usuarioId, Guid id, CategoriaRequest request, CancellationToken cancellationToken = default)
    {
        var categoria = await db.CategoriasDeModelo.FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuarioId, cancellationToken);
        if (categoria is null)
        {
            return Erro.NaoEncontrado();
        }

        var nome = request.Nome!.Trim();
        if (await NomeEmUsoAsync(usuarioId, nome, ignorarId: id, cancellationToken))
        {
            return Erro.Conflito(NomeRepetido);
        }

        categoria.Nome = nome;
        if (!await SalvarAsync(cancellationToken))
        {
            return Erro.Conflito(NomeRepetido);
        }

        var quantidade = await db.ModelosDeToken.CountAsync(m => m.CategoriaId == id, cancellationToken);
        return new CategoriaDto(categoria.Id, categoria.Nome, quantidade);
    }

    public async Task<Resultado> ExcluirCategoriaAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken = default)
    {
        var categoria = await db.CategoriasDeModelo.FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuarioId, cancellationToken);
        if (categoria is null)
        {
            return Erro.NaoEncontrado();
        }

        // A chave estrangeira (ON DELETE SET NULL) deixa os modelos sem categoria.
        db.CategoriasDeModelo.Remove(categoria);
        await db.SaveChangesAsync(cancellationToken);
        return Resultado.Ok;
    }

    public async Task<IReadOnlyList<ModeloDeTokenDto>> ListarModelosAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var modelos = await db.ModelosDeToken
            .AsNoTracking()
            .Where(m => m.UsuarioId == usuarioId)
            .OrderBy(m => m.Nome)
            .ThenBy(m => m.CriadoEm)
            .ToListAsync(cancellationToken);

        return modelos.Select(ModeloDeTokenDto.De).ToList();
    }

    public async Task<Resultado<ModeloDeTokenDto>> ObterModeloAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken = default)
    {
        var modelo = await db.ModelosDeToken.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id && m.UsuarioId == usuarioId, cancellationToken);
        return modelo is null ? Erro.NaoEncontrado() : ModeloDeTokenDto.De(modelo);
    }

    public async Task<Resultado<ModeloDeTokenDto>> CriarModeloAsync(Guid usuarioId, ModeloDeTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (await db.ModelosDeToken.CountAsync(m => m.UsuarioId == usuarioId, cancellationToken) >= ModeloDeToken.LimitePorUsuario)
        {
            return Erro.Conflito($"Você já tem {ModeloDeToken.LimitePorUsuario} modelos, que é o limite. Exclua algum para criar outro.");
        }

        if (await ValidarReferenciasAsync(usuarioId, request, cancellationToken) is { } erro)
        {
            return erro;
        }

        var agora = relogio.GetUtcNow();
        var modelo = new ModeloDeToken
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            CriadoEm = agora,
            AtualizadoEm = agora,
            ExibicaoHp = request.ExibicaoHp ?? Token.ExibicaoHpPadrao(request.Tipo!.Value)
        };
        Aplicar(modelo, request);

        db.ModelosDeToken.Add(modelo);
        await db.SaveChangesAsync(cancellationToken);
        return ModeloDeTokenDto.De(modelo);
    }

    public async Task<Resultado<ModeloDeTokenDto>> AtualizarModeloAsync(Guid usuarioId, Guid id, ModeloDeTokenRequest request, CancellationToken cancellationToken = default)
    {
        var modelo = await db.ModelosDeToken.FirstOrDefaultAsync(m => m.Id == id && m.UsuarioId == usuarioId, cancellationToken);
        if (modelo is null)
        {
            return Erro.NaoEncontrado();
        }

        if (await ValidarReferenciasAsync(usuarioId, request, cancellationToken) is { } erro)
        {
            return erro;
        }

        Aplicar(modelo, request);
        if (request.ExibicaoHp is { } exibicao)
        {
            modelo.ExibicaoHp = exibicao;
        }

        modelo.AtualizadoEm = relogio.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return ModeloDeTokenDto.De(modelo);
    }

    public async Task<Resultado> ExcluirModeloAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken = default)
    {
        var modelo = await db.ModelosDeToken.FirstOrDefaultAsync(m => m.Id == id && m.UsuarioId == usuarioId, cancellationToken);
        if (modelo is null)
        {
            return Erro.NaoEncontrado();
        }

        db.ModelosDeToken.Remove(modelo);
        await db.SaveChangesAsync(cancellationToken);
        return Resultado.Ok;
    }

    /// <summary>Categoria e imagem precisam ser do próprio usuário (a imagem, da galeria pessoal).</summary>
    private async Task<Erro?> ValidarReferenciasAsync(Guid usuarioId, ModeloDeTokenRequest request, CancellationToken cancellationToken)
    {
        if (request.CategoriaId is { } categoriaId
            && !await db.CategoriasDeModelo.AnyAsync(c => c.Id == categoriaId && c.UsuarioId == usuarioId, cancellationToken))
        {
            return Erro.Invalido("categoriaId", "Escolha uma das suas categorias.");
        }

        if (request.ImagemId is { } imagemId
            && !await db.Imagens.AnyAsync(i => i.Id == imagemId && i.UsuarioId == usuarioId, cancellationToken))
        {
            return Erro.Invalido("imagemId", "Escolha uma imagem da sua galeria da biblioteca.");
        }

        return null;
    }

    private Task<bool> NomeEmUsoAsync(Guid usuarioId, string nome, Guid? ignorarId, CancellationToken cancellationToken)
    {
        var minusculo = nome.ToLower();
        return db.CategoriasDeModelo.AnyAsync(
            c => c.UsuarioId == usuarioId && c.Id != ignorarId && c.Nome.ToLower() == minusculo, cancellationToken);
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

    // O request já passou pelo ModeloDeTokenRequestValidator; aqui só normalizamos.
    private static void Aplicar(ModeloDeToken modelo, ModeloDeTokenRequest request)
    {
        modelo.CategoriaId = request.CategoriaId;
        modelo.Tipo = request.Tipo!.Value;
        modelo.Nome = request.Nome!.Trim();
        modelo.HpMaximo = request.HpMaximo;
        modelo.Icone = request.Icone!;
        modelo.ImagemId = request.ImagemId;
        modelo.Deslocamento = request.Deslocamento;
        modelo.Visao = request.Visao;
        modelo.CorBorda = string.IsNullOrEmpty(request.CorBorda) ? Token.CorBordaPadrao : request.CorBorda.ToUpperInvariant();
    }
}
