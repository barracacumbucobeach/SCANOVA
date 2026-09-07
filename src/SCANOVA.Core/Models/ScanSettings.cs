using SCANOVA.Core.Enums;

namespace SCANOVA.Core.Models;

/// <summary>
/// Configuração de uma digitalização, correspondendo a um dos perfis de digitalização (seção 11).
/// É um <c>record</c> para permitir ajustar campos individuais (ex.: <c>Source</c>) a partir de
/// um perfil pré-configurado via <c>with</c>, sem precisar reconstruir tudo manualmente.
/// </summary>
public sealed record ScanSettings
{
    public required string ScannerId { get; init; }
    public ScanSource Source { get; init; } = ScanSource.Automatic;
    public double Dpi { get; init; } = 200;
    public ColorMode ColorMode { get; init; } = ColorMode.BlackAndWhite1Bit;

    /// <summary>Nome do perfil selecionado (ex.: "TIFF Documental"), para exibição/histórico.</summary>
    public string ProfileName { get; init; } = "TIFF Documental";

    public static ScanSettings TiffDocumental(string scannerId) => new()
    {
        ScannerId = scannerId,
        Source = ScanSource.Automatic,
        Dpi = 200,
        ColorMode = ColorMode.BlackAndWhite1Bit,
        ProfileName = "TIFF Documental",
    };

    public static ScanSettings PdfDocumental(string scannerId) => new()
    {
        ScannerId = scannerId,
        Source = ScanSource.Automatic,
        Dpi = 200,
        ColorMode = ColorMode.BlackAndWhite1Bit,
        ProfileName = "PDF Documental",
    };

    public static ScanSettings ReadableDocument(string scannerId) => new()
    {
        ScannerId = scannerId,
        Source = ScanSource.Automatic,
        Dpi = 300,
        ColorMode = ColorMode.Grayscale,
        ProfileName = "Documento Legível",
    };

    public static ScanSettings Colored(string scannerId) => new()
    {
        ScannerId = scannerId,
        Source = ScanSource.Automatic,
        Dpi = 300,
        ColorMode = ColorMode.Color,
        ProfileName = "Colorido",
    };
}
