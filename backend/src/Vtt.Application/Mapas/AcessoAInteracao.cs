using Microsoft.EntityFrameworkCore;
using Vtt.Application.Common;
using Vtt.Domain.Mapas;

namespace Vtt.Application.Mapas;

/// <summary>
/// Quem, além do Mestre, pode ver o conteúdo de uma Interação. A mesma regra vale para o pedido do
/// conteúdo e para o download da imagem dele: o jogador cujo token está ao lado, ou todos quando
/// o Mestre a revelou.
/// </summary>
internal static class AcessoAInteracao
{
    public const string Aproxime = "Aproxime-se do objeto para examiná-lo.";

    /// <summary>true se o jogador pode examinar a Interação agora. Não confere o papel nem se o mapa está ativo.</summary>
    public static async Task<bool> JogadorPodeExaminarAsync(
        this IAppDbContext db, Token interacao, Guid usuarioId, CancellationToken cancellationToken)
    {
        if (!interacao.EhInteracao || !interacao.Visivel)
        {
            return false;
        }

        if (interacao.Revelado)
        {
            return true;
        }

        var doJogador = await db.Tokens.AsNoTracking()
            .FirstOrDefaultAsync(t => t.MapaId == interacao.MapaId && t.DonoUsuarioId == usuarioId, cancellationToken);
        return interacao.PodeSerExaminadaPor(doJogador);
    }
}
