using SCANOVA.Core.Enums;
using SCANOVA.Core.Models;

namespace SCANOVA.Licensing.Verification;

/// <summary>
/// Resultado da validação de uma chave de licença. <see cref="Info"/> só é preenchido quando a
/// assinatura foi verificada com sucesso — nunca expõe campos de um payload cuja autenticidade
/// não pôde ser confirmada, mesmo que o JSON tenha sido interpretável (seção 60: nunca confiar
/// cegamente em dado externo, mesmo "bem-formado").
/// </summary>
internal sealed record LicenseValidationOutcome(LicenseStatus Status, LicenseInfo? Info, string? UserMessage, string? TechnicalDetail)
{
    public static readonly LicenseValidationOutcome Unlicensed = new(LicenseStatus.Unlicensed, null, null, null);
}
