namespace SCANOVA.Core.Models;

/// <summary>
/// Um perfil de digitalização pré-configurado (seção 11): nome amigável exibido no dropdown de
/// perfis + fábrica das configurações reais de digitalização para um scanner específico.
/// </summary>
public sealed record ScanProfile(string Name, string Description, Func<string, ScanSettings> CreateSettings)
{
    /// <summary>Catálogo padrão de perfis (seção 11), na ordem em que devem aparecer na UI.</summary>
    public static IReadOnlyList<ScanProfile> Default { get; } = new[]
    {
        new ScanProfile(
            "TIFF Documental",
            "200 DPI · Preto e branco · 1 bit · CCITT Group 4",
            ScanSettings.TiffDocumental),
        new ScanProfile(
            "PDF Documental",
            "200 DPI · Preto e branco",
            ScanSettings.PdfDocumental),
        new ScanProfile(
            "Documento Legível",
            "300 DPI · Escala de cinza · Contraste otimizado",
            ScanSettings.ReadableDocument),
        new ScanProfile(
            "Colorido",
            "300 DPI · RGB",
            ScanSettings.Colored),
    };
}
