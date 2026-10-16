using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vtt.Application.Common;
using Vtt.Application.Mapas;
using Vtt.Application.Mesas;
using Vtt.Domain.Imagens;

namespace Vtt.Application.Imagens;

public class ImagemService(
    IAppDbContext db,
    IFileStorage arquivos,
    IImagemProcessor processador,
    IOptions<UploadOptions> opcoes,
    TimeProvider relogio,
    ILogger<ImagemService> logger) : IImagemService
{
    private const string Campo = "arquivo";
    private const string ImagemDoModeloAusente = "A imagem do modelo não foi encontrada.";
    private readonly UploadOptions _limites = opcoes.Value;

    public async Task<Resultado<ImagemDto>> EnviarAsync(Guid usuarioId, Guid mesaId, string? nomeDoArquivo, Stream conteudo, long tamanho, CancellationToken cancellationToken = default)
    {
        var erro = await db.ExigirMestreAsync(usuarioId, mesaId, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        return await GravarAsync(EscopoDaMesa(mesaId), usuarioId, nomeDoArquivo, conteudo, tamanho, cancellationToken);
    }

    public Task<Resultado<ImagemDto>> EnviarPessoalAsync(Guid usuarioId, string? nomeDoArquivo, Stream conteudo, long tamanho, CancellationToken cancellationToken = default) =>
        GravarAsync(EscopoPessoal(usuarioId), usuarioId, nomeDoArquivo, conteudo, tamanho, cancellationToken);

    public async Task<Resultado<GaleriaDto>> ListarAsync(Guid usuarioId, Guid mesaId, CancellationToken cancellationToken = default)
    {
        var erro = await db.ExigirMestreAsync(usuarioId, mesaId, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        return await MontarGaleriaAsync(EscopoDaMesa(mesaId), cancellationToken);
    }

    public Task<GaleriaDto> ListarPessoaisAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        MontarGaleriaAsync(EscopoPessoal(usuarioId), cancellationToken);

    public async Task<Resultado<ArquivoDeImagem>> AbrirAsync(Guid usuarioId, Guid imagemId, bool miniatura, CancellationToken cancellationToken = default)
    {
        var imagem = await db.Imagens.AsNoTracking().FirstOrDefaultAsync(i => i.Id == imagemId, cancellationToken);
        if (imagem is null || !await PodeVerAsync(usuarioId, imagem, cancellationToken))
        {
            return Erro.NaoEncontrado();
        }

        var conteudo = await arquivos.AbrirAsync(miniatura ? imagem.CaminhoDaMiniatura : imagem.CaminhoArmazenado, cancellationToken);
        if (conteudo is null)
        {
            logger.LogError("Arquivo da imagem {ImagemId} não está no armazenamento", imagemId);
            return Erro.NaoEncontrado();
        }

        // O conteúdo de uma imagem nunca muda depois de gravado, então o id serve de ETag.
        return miniatura
            ? new ArquivoDeImagem(conteudo, TipoDeImagem.Webp.ContentType, $"\"{imagem.Id:N}-mini\"")
            : new ArquivoDeImagem(conteudo, imagem.ContentType, $"\"{imagem.Id:N}\"");
    }

    public async Task<Resultado> ExcluirAsync(Guid usuarioId, Guid imagemId, CancellationToken cancellationToken = default)
    {
        var imagem = await db.Imagens.FirstOrDefaultAsync(i => i.Id == imagemId, cancellationToken);
        if (imagem is null)
        {
            return Erro.NaoEncontrado();
        }

        if (imagem.Pessoal)
        {
            if (imagem.UsuarioId != usuarioId)
            {
                return Erro.NaoEncontrado();
            }
        }
        else
        {
            var erro = await db.ExigirMestreAsync(usuarioId, imagem.MesaId!.Value, cancellationToken);
            if (erro is not null)
            {
                return erro;
            }
        }

        var usos = await ListarUsosAsync(imagemId, cancellationToken);
        if (usos.Count > 0)
        {
            return Erro.Conflito($"Esta imagem está em uso: {string.Join("; ", usos)}. Troque ou remova a imagem nesses lugares antes de excluir.");
        }

        db.Imagens.Remove(imagem);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Passou a ser usada entre a checagem e a exclusão; a chave estrangeira segurou.
            return Erro.Conflito("Esta imagem acabou de ser colocada em uso. Recarregue e tente de novo.");
        }

        await ExcluirArquivosAsync(imagem);
        return Resultado.Ok;
    }

    public async Task<Resultado<Guid>> ObterCopiaNaMesaAsync(Guid usuarioId, Guid imagemPessoalId, Guid mesaId, CancellationToken cancellationToken = default)
    {
        if (await BuscarCopiaAsync(imagemPessoalId, mesaId, cancellationToken) is { } existente)
        {
            return existente;
        }

        var original = await db.Imagens.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == imagemPessoalId && i.UsuarioId == usuarioId, cancellationToken);
        if (original is null)
        {
            return Erro.NaoEncontrado(ImagemDoModeloAusente);
        }

        var escopo = EscopoDaMesa(mesaId);
        var uso = await ObterUsoAsync(escopo, cancellationToken);
        if (VerificarCota(escopo, uso, original.TamanhoBytes) is { } estouro)
        {
            return estouro;
        }

        var conteudo = await LerArquivoAsync(original.CaminhoArmazenado, cancellationToken);
        var miniatura = await LerArquivoAsync(original.CaminhoDaMiniatura, cancellationToken);
        if (conteudo is null || miniatura is null)
        {
            logger.LogError("Arquivo da imagem pessoal {ImagemId} não está no armazenamento", imagemPessoalId);
            return Erro.NaoEncontrado(ImagemDoModeloAusente);
        }

        var id = Guid.NewGuid();
        var extensao = original.CaminhoArmazenado[(original.CaminhoArmazenado.LastIndexOf('.') + 1)..];
        var copia = new ImagemUpload
        {
            Id = id,
            MesaId = mesaId,
            EnviadaPorUsuarioId = usuarioId,
            OrigemImagemId = original.Id,
            NomeOriginal = original.NomeOriginal,
            ContentType = original.ContentType,
            TamanhoBytes = original.TamanhoBytes,
            Largura = original.Largura,
            Altura = original.Altura,
            CaminhoArmazenado = ImagemUpload.CaminhoPara(mesaId, id, extensao),
            CriadaEm = relogio.GetUtcNow()
        };

        await arquivos.SalvarAsync(copia.CaminhoArmazenado, conteudo, cancellationToken);
        await arquivos.SalvarAsync(copia.CaminhoDaMiniatura, miniatura, cancellationToken);

        db.Imagens.Add(copia);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Outra colocação copiou a mesma imagem ao mesmo tempo; o índice único segurou e a dela vale.
            db.Imagens.Entry(copia).State = EntityState.Detached;
            await ExcluirArquivosAsync(copia);
            if (await BuscarCopiaAsync(imagemPessoalId, mesaId, cancellationToken) is { } vencedora)
            {
                return vencedora;
            }

            throw;
        }

        return id;
    }

    private async Task<Resultado<ImagemDto>> GravarAsync(Escopo escopo, Guid usuarioId, string? nomeDoArquivo, Stream conteudo, long tamanho, CancellationToken cancellationToken)
    {
        if (tamanho <= 0)
        {
            return Rejeitar(escopo, "Escolha um arquivo de imagem.");
        }

        if (tamanho > _limites.TamanhoMaximoBytes)
        {
            return Rejeitar(escopo, $"O arquivo passa do limite de {EmMegabytes(_limites.TamanhoMaximoBytes)} MB.");
        }

        var dados = await LerAsync(conteudo, _limites.TamanhoMaximoBytes, cancellationToken);
        if (dados is null)
        {
            return Rejeitar(escopo, $"O arquivo passa do limite de {EmMegabytes(_limites.TamanhoMaximoBytes)} MB.");
        }

        if (TipoDeImagem.Detectar(dados) is not { } tipo)
        {
            return Rejeitar(escopo, "O arquivo não é uma imagem PNG, JPEG, WebP ou GIF.");
        }

        if (processador.LerDimensoes(dados, tipo) is not { } dimensoes)
        {
            return Rejeitar(escopo, "Não foi possível ler a imagem. O arquivo pode estar corrompido.");
        }

        if (dimensoes.Largura > _limites.DimensaoMaxima || dimensoes.Altura > _limites.DimensaoMaxima)
        {
            return Rejeitar(escopo, $"A imagem tem {dimensoes.Largura} × {dimensoes.Altura} pixels; o máximo é {_limites.DimensaoMaxima} × {_limites.DimensaoMaxima}.");
        }

        // Uma imagem parada já está contida pela dimensão máxima; o limite vale para a soma dos quadros.
        if (dimensoes.Quadros > 1 && (long)dimensoes.Largura * dimensoes.Altura * dimensoes.Quadros > _limites.PixelsMaximosDaAnimacao)
        {
            var quadrosPermitidos = Math.Max(1, _limites.PixelsMaximosDaAnimacao / ((long)dimensoes.Largura * dimensoes.Altura));
            return Rejeitar(escopo, $"A animação tem {dimensoes.Quadros} quadros de {dimensoes.Largura} × {dimensoes.Altura} pixels; nesse tamanho, o máximo é {quadrosPermitidos} quadros. Reduza a imagem ou a quantidade de quadros.");
        }

        var uso = await ObterUsoAsync(escopo, cancellationToken);
        if (VerificarCota(escopo, uso, bytesNovos: 0) is { } cheia)
        {
            return cheia;
        }

        if (processador.Recodificar(dados, tipo, ImagemUpload.LadoDaMiniatura) is not { } processada)
        {
            return Rejeitar(escopo, "Não foi possível ler a imagem. O arquivo pode estar corrompido.");
        }

        if (VerificarCota(escopo, uso, processada.Conteudo.LongLength) is { } estouro)
        {
            return estouro;
        }

        var id = Guid.NewGuid();
        var imagem = new ImagemUpload
        {
            Id = id,
            MesaId = escopo.MesaId,
            UsuarioId = escopo.UsuarioId,
            EnviadaPorUsuarioId = usuarioId,
            NomeOriginal = SanitizarNome(nomeDoArquivo),
            ContentType = tipo.ContentType,
            TamanhoBytes = processada.Conteudo.LongLength,
            Largura = processada.Largura,
            Altura = processada.Altura,
            CaminhoArmazenado = escopo.MesaId is { } mesaId
                ? ImagemUpload.CaminhoPara(mesaId, id, tipo.Extensao)
                : ImagemUpload.CaminhoPessoalPara(escopo.UsuarioId!.Value, id, tipo.Extensao),
            CriadaEm = relogio.GetUtcNow()
        };

        await arquivos.SalvarAsync(imagem.CaminhoArmazenado, processada.Conteudo, cancellationToken);
        await arquivos.SalvarAsync(imagem.CaminhoDaMiniatura, processada.Miniatura, cancellationToken);

        db.Imagens.Add(imagem);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Sem a linha no banco os arquivos ficariam órfãos.
            await ExcluirArquivosAsync(imagem);
            throw;
        }

        return ImagemDto.De(imagem);
    }

    private async Task<GaleriaDto> MontarGaleriaAsync(Escopo escopo, CancellationToken cancellationToken)
    {
        var imagens = await DoEscopo(escopo)
            .AsNoTracking()
            .OrderByDescending(imagem => imagem.CriadaEm)
            .ToListAsync(cancellationToken);

        return new GaleriaDto(
            imagens.Select(ImagemDto.De).ToList(),
            imagens.Sum(imagem => imagem.TamanhoBytes),
            escopo.CotaBytes,
            escopo.CotaImagens,
            _limites.TamanhoMaximoBytes,
            _limites.DimensaoMaxima);
    }

    /// <summary>Imagem de mesa: qualquer membro. Imagem pessoal: só o dono.</summary>
    private async Task<bool> PodeVerAsync(Guid usuarioId, ImagemUpload imagem, CancellationToken cancellationToken) =>
        imagem.Pessoal
            ? imagem.UsuarioId == usuarioId
            : await db.ObterPapelAsync(usuarioId, imagem.MesaId!.Value, cancellationToken) is not null;

    private Task<Guid?> BuscarCopiaAsync(Guid imagemPessoalId, Guid mesaId, CancellationToken cancellationToken) =>
        db.Imagens
            .Where(i => i.MesaId == mesaId && i.OrigemImagemId == imagemPessoalId)
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<byte[]?> LerArquivoAsync(string caminho, CancellationToken cancellationToken)
    {
        await using var arquivo = await arquivos.AbrirAsync(caminho, cancellationToken);
        if (arquivo is null)
        {
            return null;
        }

        using var memoria = new MemoryStream();
        await arquivo.CopyToAsync(memoria, cancellationToken);
        return memoria.ToArray();
    }

    /// <summary>A mesa ou a galeria pessoal: onde a imagem fica e que cota vale.</summary>
    private sealed record Escopo(Guid? MesaId, Guid? UsuarioId, long CotaBytes, int CotaImagens, string Sujeito);

    private Escopo EscopoDaMesa(Guid mesaId) =>
        new(mesaId, null, _limites.CotaBytesPorMesa, _limites.CotaImagensPorMesa, "A mesa");

    private Escopo EscopoPessoal(Guid usuarioId) =>
        new(null, usuarioId, _limites.CotaBytesPorUsuario, _limites.CotaImagensPorUsuario, "A sua galeria");

    private IQueryable<ImagemUpload> DoEscopo(Escopo escopo) =>
        escopo.MesaId is { } mesaId
            ? db.Imagens.Where(imagem => imagem.MesaId == mesaId)
            : db.Imagens.Where(imagem => imagem.UsuarioId == escopo.UsuarioId);

    /// <param name="bytesNovos">0 confere só a quantidade; com bytes, confere também o espaço.</param>
    private Erro? VerificarCota(Escopo escopo, (int Quantidade, long Bytes) uso, long bytesNovos)
    {
        if (uso.Quantidade >= escopo.CotaImagens)
        {
            return Erro.Conflito($"{escopo.Sujeito} já tem {uso.Quantidade} imagens, que é o limite. Exclua alguma para enviar outra.");
        }

        if (bytesNovos > 0 && uso.Bytes + bytesNovos > escopo.CotaBytes)
        {
            return Erro.Conflito($"{escopo.Sujeito} passaria do limite de {EmMegabytes(escopo.CotaBytes)} MB em imagens. Exclua alguma para enviar outra.");
        }

        return null;
    }

    private async Task<List<string>> ListarUsosAsync(Guid imagemId, CancellationToken cancellationToken)
    {
        var usos = new List<string>();

        var mapas = await db.Mapas
            .Where(mapa => mapa.ImagemFundoId == imagemId)
            .Select(mapa => mapa.Nome)
            .ToListAsync(cancellationToken);
        usos.AddRange(mapas.Select(nome => $"fundo do mapa \"{nome}\""));

        var tokens = await db.Tokens
            .Where(token => token.ImagemId == imagemId)
            .Join(db.Mapas, token => token.MapaId, mapa => mapa.Id, (token, mapa) => new { Token = token.Nome, Mapa = mapa.Nome })
            .ToListAsync(cancellationToken);
        usos.AddRange(tokens.Select(uso => $"token \"{uso.Token}\" no mapa \"{uso.Mapa}\""));

        var estruturas = await db.Estruturas
            .Where(estrutura => estrutura.TexturaImagemId == imagemId)
            .Join(db.Mapas, estrutura => estrutura.MapaId, mapa => mapa.Id, (_, mapa) => mapa.Nome)
            .ToListAsync(cancellationToken);
        usos.AddRange(estruturas
            .GroupBy(nome => nome)
            .Select(grupo => grupo.Count() == 1
                ? $"textura de 1 estrutura no mapa \"{grupo.Key}\""
                : $"textura de {grupo.Count()} estruturas no mapa \"{grupo.Key}\""));

        var modelos = await db.ModelosDeToken
            .Where(modelo => modelo.ImagemId == imagemId)
            .Select(modelo => modelo.Nome)
            .ToListAsync(cancellationToken);
        usos.AddRange(modelos.Select(nome => $"modelo \"{nome}\" da biblioteca"));

        return usos;
    }

    private async Task<(int Quantidade, long Bytes)> ObterUsoAsync(Escopo escopo, CancellationToken cancellationToken)
    {
        var uso = await DoEscopo(escopo)
            .GroupBy(_ => 1)
            .Select(grupo => new { Quantidade = grupo.Count(), Bytes = grupo.Sum(imagem => imagem.TamanhoBytes) })
            .FirstOrDefaultAsync(cancellationToken);
        return uso is null ? (0, 0) : (uso.Quantidade, uso.Bytes);
    }

    private async Task ExcluirArquivosAsync(ImagemUpload imagem)
    {
        try
        {
            await arquivos.ExcluirAsync(imagem.CaminhoArmazenado);
            await arquivos.ExcluirAsync(imagem.CaminhoDaMiniatura);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao apagar os arquivos da imagem {ImagemId}", imagem.Id);
        }
    }

    private Erro Rejeitar(Escopo escopo, string motivo)
    {
        logger.LogInformation("Upload rejeitado (mesa {MesaId}, usuário {UsuarioId}): {Motivo}", escopo.MesaId, escopo.UsuarioId, motivo);
        return Erro.Invalido(Campo, motivo);
    }

    /// <summary>Lê o conteúdo inteiro, ou devolve null se ele passar do limite (o tamanho declarado pode mentir).</summary>
    private static async Task<byte[]?> LerAsync(Stream conteudo, long limite, CancellationToken cancellationToken)
    {
        using var memoria = new MemoryStream();
        var buffer = new byte[81920];
        int lidos;
        while ((lidos = await conteudo.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (memoria.Length + lidos > limite)
            {
                return null;
            }

            memoria.Write(buffer, 0, lidos);
        }

        return memoria.ToArray();
    }

    /// <summary>Só o nome base, sem caracteres de controle nem separadores de caminho, limitado no tamanho.</summary>
    internal static string SanitizarNome(string? nome)
    {
        var limpo = new string((nome ?? string.Empty)
            .Replace('\\', '/')
            .Split('/')
            .Last()
            .Where(caractere => !char.IsControl(caractere) && caractere is not ('<' or '>' or ':' or '"' or '|' or '?' or '*'))
            .ToArray()).Trim().Trim('.');

        if (limpo.Length == 0)
        {
            return "imagem";
        }

        return limpo.Length <= ImagemUpload.NomeOriginalTamanhoMaximo ? limpo : limpo[..ImagemUpload.NomeOriginalTamanhoMaximo];
    }

    private static string EmMegabytes(long bytes) => (bytes / (1024d * 1024d)).ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
}
