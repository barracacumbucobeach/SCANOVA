using SCANOVA.Core.Enums;

namespace SCANOVA.Core.Models;

/// <summary>
/// Informações de uma licença vitalícia do SCANOVA (ver seção 55). Não contém nenhum segredo
/// criptográfico — apenas os dados descritivos da licença e seu estado de validação local.
/// </summary>
public sealed class LicenseInfo
{
    public required string LicenseId { get; init; }
    public required string Product { get; init; }
    public required string Edition { get; init; }
    public string? CustomerName { get; init; }
    public int ActivationLimit { get; init; } = 1;
    public DateTime CreatedAt { get; init; }
    public required LicenseStatus Status { get; init; }
}
