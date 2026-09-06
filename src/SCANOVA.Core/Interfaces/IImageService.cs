using SCANOVA.Core.Models;

namespace SCANOVA.Core.Interfaces;

/// <summary>
/// Operações fundamentais de manipulação de imagem usadas pelo editor e pelos pipelines de
/// processamento (crop, rotação, espelhamento, conversões de cor, redimensionamento por DPI).
/// </summary>
public interface IImageService
{
    /// <summary>Gira a imagem por um ângulo arbitrário (não limitado a múltiplos de 90°), expandindo a tela para caber o resultado.</summary>
    RasterImage Rotate(RasterImage image, double degrees);

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

    /// <summary>
    /// Corrige a perspectiva de um quadrilátero (possivelmente não retangular, por foto/scan
    /// inclinado) para um retângulo alinhado aos eixos com as dimensões informadas, via
    /// transformação projetiva (homografia) — seção 17.
    /// </summary>
    RasterImage CorrectPerspective(RasterImage image, CropRegion quad, int outputWidth, int outputHeight);

    /// <summary>Ajusta brilho (-100..100) e contraste (-100..100) de forma linear. 0/0 = sem alteração.</summary>
    RasterImage AdjustBrightnessContrast(RasterImage image, int brightness, int contrast);

    /// <summary>Aplica correção de gamma. 1.0 = sem alteração; menor que 1 escurece, maior que 1 clareia os tons médios.</summary>
    RasterImage AdjustGamma(RasterImage image, double gamma);

    /// <summary>Aumenta nitidez via máscara de nitidez (unsharp mask). 0 = sem alteração.</summary>
    RasterImage Sharpen(RasterImage image, double amount);

    /// <summary>Reduz ruído por suavização leve (filtro de média), preservando bordas o quanto possível.</summary>
    RasterImage ReduceNoise(RasterImage image, int radius = 1);

    /// <summary>
    /// Normaliza iluminação/fundo irregular (ex.: sombra de foto de celular): estima o fundo por
    /// desfoque de grande raio e o remove, preservando texto/tinta em primeiro plano (seção 19/20).
    /// Requer uma imagem em escala de cinza.
    /// </summary>
    RasterImage RemoveBackground(RasterImage grayscaleImage, int blurRadius = 15);

    /// <summary>
    /// Ajusta a saturação de cor (-100..100; 0 = sem alteração; -100 = tons de cinza; 100 =
    /// saturação dobrada), interpolando cada canal em direção à sua luminância. Sem efeito em
    /// imagens que já não estão em modo colorido (<see cref="Enums.PixelFormat.Gray8"/>/
    /// <see cref="Enums.PixelFormat.Bilevel1"/>) — retorna a imagem original nesse caso.
    /// </summary>
    RasterImage AdjustSaturation(RasterImage image, int saturation);
}
