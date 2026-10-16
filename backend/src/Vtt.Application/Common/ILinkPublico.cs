namespace Vtt.Application.Common;

/// <summary>Monta URLs que os jogadores abrem no navegador, a partir da URL pública configurada.</summary>
public interface ILinkPublico
{
    string Convite(string codigo);
}
