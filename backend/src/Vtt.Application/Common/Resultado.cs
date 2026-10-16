namespace Vtt.Application.Common;

public enum TipoErro
{
    /// <summary>O recurso não existe ou o usuário não pode saber que existe (404).</summary>
    NaoEncontrado,

    /// <summary>O usuário vê o recurso, mas o papel dele não permite a ação (403).</summary>
    Proibido,

    /// <summary>A ação é válida, mas o estado atual não a permite (409).</summary>
    Conflito,

    /// <summary>Um dado enviado não é aceito (400, associado a um campo).</summary>
    Invalido
}

public record Erro(TipoErro Tipo, string Mensagem, string? Campo = null)
{
    public static Erro NaoEncontrado(string mensagem = "Não encontrado.") => new(TipoErro.NaoEncontrado, mensagem);

    public static Erro Proibido(string mensagem) => new(TipoErro.Proibido, mensagem);

    public static Erro Conflito(string mensagem) => new(TipoErro.Conflito, mensagem);

    public static Erro Invalido(string campo, string mensagem) => new(TipoErro.Invalido, mensagem, campo);
}

/// <summary>Resultado de uma operação sem retorno: sucesso ou um <see cref="Common.Erro"/>.</summary>
public readonly record struct Resultado(Erro? Erro)
{
    public bool Sucesso => Erro is null;

    public static Resultado Ok => new(null);

    public static implicit operator Resultado(Erro erro) => new(erro);
}

/// <summary>Resultado de uma operação: o valor ou um <see cref="Common.Erro"/>.</summary>
public readonly record struct Resultado<T>(T? Valor, Erro? Erro)
{
    public bool Sucesso => Erro is null;

    public static implicit operator Resultado<T>(T valor) => new(valor, null);

    public static implicit operator Resultado<T>(Erro erro) => new(default, erro);
}
