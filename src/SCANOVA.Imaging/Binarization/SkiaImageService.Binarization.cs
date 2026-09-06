using SCANOVA.Core.Models;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;

namespace SCANOVA.Imaging.ImageProcessing;

/// <summary>
/// Binarização (conversão para preto e branco 1-bit) — passo central do pipeline TIFF
/// documental (seção 22/23/85). Algoritmos puros sobre o buffer de pixels em escala de cinza,
/// sem dependência de SkiaSharp.
/// </summary>
public sealed partial class SkiaImageService
{
    public byte ComputeOtsuThreshold(RasterImage grayscaleImage)
    {
        RequireGray8(grayscaleImage);

        var histogram = new int[256];
        for (var y = 0; y < grayscaleImage.Height; y++)
        {
            var rowStart = y * grayscaleImage.Stride;
            for (var x = 0; x < grayscaleImage.Width; x++)
            {
                histogram[grayscaleImage.Pixels[rowStart + x]]++;
            }
        }

        var total = grayscaleImage.Width * grayscaleImage.Height;
        if (total == 0)
        {
            return 128;
        }

        double sumAll = 0;
        for (var i = 0; i < 256; i++)
        {
            sumAll += i * histogram[i];
        }

        double sumBackground = 0;
        var weightBackground = 0;
        double maxBetweenClassVariance = 0;
        byte bestThreshold = 128;

        // Método de Otsu (1979): varre todos os limiares possíveis e escolhe o que maximiza a
        // variância entre as duas classes (fundo/tinta) do histograma — equivalente a minimizar
        // a variância dentro de cada classe.
        for (var t = 0; t < 256; t++)
        {
            weightBackground += histogram[t];
            if (weightBackground == 0)
            {
                continue;
            }

            var weightForeground = total - weightBackground;
            if (weightForeground == 0)
            {
                break;
            }

            sumBackground += t * (double)histogram[t];

            var meanBackground = sumBackground / weightBackground;
            var meanForeground = (sumAll - sumBackground) / weightForeground;
            var meanDifference = meanBackground - meanForeground;

            var betweenClassVariance = (double)weightBackground * weightForeground * meanDifference * meanDifference;

            if (betweenClassVariance > maxBetweenClassVariance)
            {
                maxBetweenClassVariance = betweenClassVariance;
                bestThreshold = (byte)t;
            }
        }

        return bestThreshold;
    }

    public RasterImage Binarize(RasterImage grayscaleImage, byte threshold)
    {
        RequireGray8(grayscaleImage);

        var width = grayscaleImage.Width;
        var height = grayscaleImage.Height;
        var stride = RasterImage.MinimumStride(width, CorePixelFormat.Bilevel1);
        var packed = new byte[stride * height];

        for (var y = 0; y < height; y++)
        {
            var srcRow = y * grayscaleImage.Stride;
            var dstRow = y * stride;
            for (var x = 0; x < width; x++)
            {
                var gray = grayscaleImage.Pixels[srcRow + x];
                // Convenção do RasterImage (seção Enums.PixelFormat.Bilevel1): bit 1 = preto
                // (tinta). Pixels mais escuros que o limiar viram tinta.
                if (gray < threshold)
                {
                    packed[dstRow + (x >> 3)] |= (byte)(0x80 >> (x & 7));
                }
            }
        }

        return new RasterImage(width, height, stride, CorePixelFormat.Bilevel1, packed, grayscaleImage.HorizontalDpi, grayscaleImage.VerticalDpi);
    }

    public RasterImage BinarizeAdaptive(RasterImage grayscaleImage, int windowSize = 25, double sensitivity = 0.15)
    {
        RequireGray8(grayscaleImage);
        if (windowSize < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(windowSize), "A janela deve ter pelo menos 3 pixels.");
        }

        var width = grayscaleImage.Width;
        var height = grayscaleImage.Height;

        // Imagem integral (soma de prefixos 2D) para calcular a média de qualquer janela
        // retangular em tempo O(1) — algoritmo de limiar adaptativo de Bradley (2007),
        // "Adaptive Thresholding Using the Integral Image".
        var integral = new long[(width + 1) * (height + 1)];
        var integralRowStride = width + 1;
        for (var y = 0; y < height; y++)
        {
            var srcRow = y * grayscaleImage.Stride;
            long rowSum = 0;
            var integralPrevRow = y * integralRowStride;
            var integralCurrRow = (y + 1) * integralRowStride;
            for (var x = 0; x < width; x++)
            {
                rowSum += grayscaleImage.Pixels[srcRow + x];
                integral[integralCurrRow + x + 1] = integral[integralPrevRow + x + 1] + rowSum;
            }
        }

        var half = Math.Max(1, windowSize / 2);
        var stride = RasterImage.MinimumStride(width, CorePixelFormat.Bilevel1);
        var packed = new byte[stride * height];

        for (var y = 0; y < height; y++)
        {
            var srcRow = y * grayscaleImage.Stride;
            var dstRow = y * stride;
            var y0 = Math.Max(0, y - half);
            var y1 = Math.Min(height - 1, y + half);

            for (var x = 0; x < width; x++)
            {
                var x0 = Math.Max(0, x - half);
                var x1 = Math.Min(width - 1, x + half);

                var count = (long)(x1 - x0 + 1) * (y1 - y0 + 1);
                var sum = integral[(y1 + 1) * integralRowStride + (x1 + 1)]
                        - integral[y0 * integralRowStride + (x1 + 1)]
                        - integral[(y1 + 1) * integralRowStride + x0]
                        + integral[y0 * integralRowStride + x0];

                var localMean = sum / (double)count;
                var gray = grayscaleImage.Pixels[srcRow + x];

                // Pixel é "tinta" quando está sensivelmente mais escuro que a média local.
                if (gray < localMean * (1.0 - sensitivity))
                {
                    packed[dstRow + (x >> 3)] |= (byte)(0x80 >> (x & 7));
                }
            }
        }

        return new RasterImage(width, height, stride, CorePixelFormat.Bilevel1, packed, grayscaleImage.HorizontalDpi, grayscaleImage.VerticalDpi);
    }

    private static void RequireGray8(RasterImage image)
    {
        if (image.Format != CorePixelFormat.Gray8)
        {
            throw new ArgumentException($"Binarização requer uma imagem {CorePixelFormat.Gray8}, recebido {image.Format}.", nameof(image));
        }
    }
}
