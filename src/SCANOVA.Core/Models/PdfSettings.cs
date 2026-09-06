using SCANOVA.Core.Enums;

namespace SCANOVA.Core.Models;

/// <summary>Configuração de geração/exportação de um arquivo PDF.</summary>
public sealed class PdfSettings
{
    public required PdfMode Mode { get; init; }
    public double Dpi { get; init; } = 200;

    /// <summary>Quando <see cref="Mode"/> é <see cref="PdfMode.Searchable"/>, inclui a camada de texto OCR.</summary>
    public bool IncludeOcrTextLayer { get; init; }

    public static PdfSettings Documental { get; } = new() { Mode = PdfMode.Documental, Dpi = 200 };
}
