namespace SCANOVA.Licensing.Signing;

/// <summary>
/// Conteúdo assinado de uma chave de licença (ver <see cref="SignedLicenseEnvelope"/>) — os
/// mesmos campos de <see cref="Core.Models.LicenseInfo"/>, exceto <c>Status</c> (que nunca faz
/// parte do conteúdo assinado: é sempre calculado localmente, nunca confiado a partir de dados
/// externos). Serializado como JSON "cru", sem política de nomes (mesma convenção usada em
/// <c>JsonSettingsService</c>).
/// </summary>
internal sealed class LicensePayload
{
    public required string LicenseId { get; init; }
    public required string Product { get; init; }
    public required string Edition { get; init; }
    public string? CustomerName { get; init; }
    public int ActivationLimit { get; init; } = 1;
    public DateTime CreatedAt { get; init; }
}
