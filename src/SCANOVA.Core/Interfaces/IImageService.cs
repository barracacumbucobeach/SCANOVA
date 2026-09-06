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

    /// <summary>
    /// Calcula o limiar global ótimo pelo método de Otsu (maximiza a variância entre classes do
    /// histograma). Base do modo "Automático" de binarização (seção 89).
    /// </summary>
    byte ComputeOtsuThreshold(RasterImage grayscaleImage);

    /// <summary>
    /// Converte uma imagem em escala de cinza (<see cref="Enums.PixelFormat.Gray8"/>) para
    /// preto e branco 1-bit (<see cref="Enums.PixelFormat.Bilevel1"/>) usando um único limiar
    /// para toda a imagem. Passo central do pipeline TIFF documental (seção 23/85).
    /// </summary>
    RasterImage Binarize(RasterImage grayscaleImage, byte threshold);

    /// <summary>
    /// Converte para 1-bit usando um limiar calculado localmente (média de uma janela ao redor
    /// de cada pixel — algoritmo de Bradley), mais robusto a iluminação irregular do que um
    /// limiar único (seção 89, modo "Adaptativo").
    /// </summary>
    RasterImage BinarizeAdaptive(RasterImage grayscaleImage, int windowSize = 25, double sensitivity = 0.15);
}
