using SCANOVA.Core.Enums;
using SCANOVA.Core.Models;

namespace SCANOVA.Core.Interfaces;

/// <summary>
/// Módulo isolado de licenciamento (seção 54). Modelo comercial: licença vitalícia da versão
/// adquirida — sem assinatura, sem mensalidade, sem limite artificial de uso.
/// </summary>
public interface ILicenseService
{
    bool IsLicensed();

    LicenseStatus GetLicenseStatus();

    /// <summary>Ativa uma licença a partir da chave informada pelo usuário.</summary>
    Task<ProcessingResult> ActivateAsync(string licenseKey, CancellationToken cancellationToken = default);

    Task DeactivateAsync(CancellationToken cancellationToken = default);

    LicenseInfo? GetLicenseInfo();
}
