using SCANOVA.Core.Enums;

namespace SCANOVA.Core.Models;

/// <summary>
/// Um registro do histórico local (seção 46). Deliberadamente não guarda conteúdo do documento
/// nem dados pessoais — apenas metadados suficientes para localizar/reabrir o arquivo.
/// </summary>
public sealed class HistoryEntry
{
    public required Guid Id { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string FileName { get; init; }
    public required string FilePath { get; init; }
    public required OutputFormat Format { get; init; }
    public string? Description { get; init; } // ex.: "TIFF CCITT G4 · 200 DPI"

    /// <summary>Configuração de exportação usada, para permitir "reutilizar configuração".</summary>
    public ExportSettings? ExportSettingsUsed { get; init; }
}
