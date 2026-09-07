namespace SCANOVA.Core.Enums;

/// <summary>Modo de cor de um documento/imagem processada.</summary>
public enum ColorMode
{
    /// <summary>Cor verdadeira (RGB/RGBA), 24/32 bits por pixel.</summary>
    Color,

    /// <summary>Escala de cinza, 8 bits por pixel.</summary>
    Grayscale,

    /// <summary>Preto e branco puro, 1 bit por pixel (bilevel). Exigido pelo preset TIFF Documental.</summary>
    BlackAndWhite1Bit,
}
