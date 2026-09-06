namespace SCANOVA.Core.Enums;

/// <summary>
/// Formato de pixel de um <see cref="Models.RasterImage"/> em memória.
/// </summary>
public enum PixelFormat
{
    /// <summary>8 bits por pixel, escala de cinza.</summary>
    Gray8,

    /// <summary>24 bits por pixel, RGB (sem canal alfa).</summary>
    Rgb24,

    /// <summary>32 bits por pixel, RGBA.</summary>
    Rgba32,

    /// <summary>
    /// 1 bit por pixel, empacotado (8 pixels por byte, MSB primeiro). Convenção: bit 1 = preto (tinta),
    /// bit 0 = branco (fundo) — compatível com <see cref="Enums.CompressionType.CcittGroup4"/> usando
    /// PhotometricInterpretation = MinIsWhite.
    /// </summary>
    Bilevel1,
}
