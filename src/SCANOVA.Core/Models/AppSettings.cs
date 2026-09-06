using SCANOVA.Core.Enums;

namespace SCANOVA.Core.Models;

/// <summary>
/// Raiz das configurações persistidas do aplicativo (tela "Configurações", seção 48).
/// É um <c>record</c> para permitir atualizações imutáveis convenientes via expressões
/// <c>with</c> a partir da UI (ex.: <c>settings with { Theme = "Dark" }</c>).
/// </summary>
public sealed record AppSettings
{
    public AutomationSettings Automation { get; init; } = new();
    public TiffDefaultsSettings TiffDefaults { get; init; } = new();
    public SavingSettings Saving { get; init; } = new();
    public OcrSettings Ocr { get; init; } = OcrSettings.Default;
    public string Theme { get; init; } = "System"; // "Light" | "Dark" | "System"
    public string Language { get; init; } = "pt-BR";

    /// <summary>Verdadeiro após o usuário concluir/dispensar a introdução de primeiro uso (seção 133).</summary>
    public bool FirstRunCompleted { get; init; }
}

/// <summary>Configurações de automação (seção 50).</summary>
public sealed record AutomationSettings
{
    public bool DetectDocument { get; init; } = true;
    public bool AskBeforeCrop { get; init; } = true;
    public bool AutoCrop { get; init; }
    public bool NeverCrop { get; init; }
    public bool CorrectSkew { get; init; } = true;
    public bool AutoEnhance { get; init; }
    public bool RemoveBackground { get; init; }

    /// <summary>Confiança mínima (0.0-1.0) para sugerir/realizar corte automático (seção 13).</summary>
    public double MinimumDetectionConfidence { get; init; } = 0.6;
}

/// <summary>Configurações padrão de TIFF (seção 49).</summary>
public sealed record TiffDefaultsSettings
{
    public double Dpi { get; init; } = 200;
    public CompressionType Compression { get; init; } = CompressionType.CcittGroup4;
    public ColorMode ColorMode { get; init; } = ColorMode.BlackAndWhite1Bit;
    public bool ValidateAfterSave { get; init; } = true;
}

/// <summary>Configurações de salvamento (seção 51).</summary>
public sealed record SavingSettings
{
    public string? DefaultFolder { get; init; }
    public bool RememberLastFolder { get; init; } = true;
    public string FileNamePattern { get; init; } = "Documento_{yyyyMMdd}_{HHmmss}";
    public bool PromptBeforeOverwrite { get; init; } = true;
    public bool CreateDateSubfolders { get; init; }
}
