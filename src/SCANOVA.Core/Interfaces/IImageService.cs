using SCANOVA.Core.Models;

namespace SCANOVA.Core.Interfaces;

/// <summary>
/// Operações fundamentais de manipulação de imagem usadas pelo editor e pelos pipelines de
/// processamento (crop, rotação, espelhamento, conversões de cor, redimensionamento por DPI).
/// </summary>
public interface IImageService
{
    RasterImage Rotate(RasterImage image, int degrees);

    RasterImage FlipHorizontal(RasterImage image);

    RasterImage FlipVertical(RasterImage image);

    /// <summary>
    /// Corta a imagem pela caixa delimitadora (bounding box) da região informada. Quando a
    /// região não é um retângulo alinhado aos eixos, corrija a perspectiva primeiro (ver
    /// <c>IDocumentEnhancementService</c>) para não perder conteúdo nas bordas.
    /// </summary>
    RasterImage Crop(RasterImage image, CropRegion region);

    RasterImage ToGrayscale(RasterImage image);

    /// <summary>Redimensiona a imagem para que sua resolução efetiva passe a ser <paramref name="targetDpi"/>, preservando o tamanho físico.</summary>
    RasterImage NormalizeDpi(RasterImage image, double targetDpi);
}
