using SCANOVA.Core.Enums;

namespace SCANOVA.Core.Models;

/// <summary>Configuração de reconhecimento de texto (OCR).</summary>
public sealed class OcrSettings
{
    public OcrLanguage Language { get; init; } = OcrLanguage.PortugueseBrazil;
    public bool PreserveLineBreaks { get; init; } = true;
    public bool CorrectOrientation { get; init; } = true;

    public static OcrSettings Default { get; } = new();
}
