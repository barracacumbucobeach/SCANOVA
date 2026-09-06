namespace SCANOVA.Core.Models;

/// <summary>Um scanner descoberto no sistema (ex.: via WIA no Windows).</summary>
public sealed class ScannerInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Manufacturer { get; init; }
    public bool IsDefault { get; init; }
}
