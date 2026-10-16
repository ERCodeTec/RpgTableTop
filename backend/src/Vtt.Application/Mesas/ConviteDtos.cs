namespace Vtt.Application.Mesas;

/// <param name="ValidadeHoras">Horas até expirar; ausente = não expira.</param>
/// <param name="UsosMaximos">Quantas pessoas podem entrar por este convite; ausente = sem limite.</param>
public record ConviteRequest(int? ValidadeHoras, int? UsosMaximos);

public record ConviteDto(
    string Codigo,
    string Url,
    DateTimeOffset CriadoEm,
    DateTimeOffset? ExpiraEm,
    DateTimeOffset? RevogadoEm,
    int? UsosMaximos,
    int UsosAtuais,
    bool Valido);

public record ConvitePreviaDto(
    string Codigo,
    Guid MesaId,
    string NomeDaMesa,
    string? DescricaoDaMesa,
    string NomeDoMestre,
    int QuantidadeJogadores,
    bool JaSouMembro);

public record AceitarConviteRequest(Guid PersonagemId);

public record AceitarConviteResponse(Guid MesaId);
