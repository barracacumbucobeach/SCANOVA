namespace SCANOVA.LicenseTool;

/// <summary>
/// Espelha (deliberadamente, por uma implementação independente — ver comentário no .csproj)
/// os campos de <c>SCANOVA.Core.Models.LicenseInfo</c> que fazem parte do conteúdo assinado de
/// uma licença. Serializado como JSON "cru" (mesma convenção sem política de nomes usada pelo
/// resto do projeto, ver <c>JsonSettingsService</c>).
/// </summary>
public sealed class LicensePayload
{
    public required string LicenseId { get; init; }
    public required string Product { get; init; }
    public required string Edition { get; init; }
    public string? CustomerName { get; init; }
    public int ActivationLimit { get; init; } = 1;
    public DateTime CreatedAt { get; init; }
}
