using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SCANOVA.Imaging.ImageProcessing;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;
using Xunit;

namespace SCANOVA.Imaging.Tests;

public class BinarizationTests
{
    private readonly IImageService _sut = new SkiaImageService();

    private static RasterImage CreateGray(int width, int height, Func<int, int, byte> pixelValue)
    {
        var pixels = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                pixels[y * width + x] = pixelValue(x, y);
            }
        }

        return new RasterImage(width, height, width, CorePixelFormat.Gray8, pixels, 200, 200);
    }

    private static bool GetBit(RasterImage bilevel, int x, int y)
    {
        var bit = (bilevel.Pixels[y * bilevel.Stride + (x >> 3)] >> (7 - (x & 7))) & 1;
        return bit == 1;
    }

    [Fact]
    public void ComputeOtsuThreshold_BimodalImage_FindsThresholdBetweenClusters()
    {
        // Metade esquerda escura (~30), metade direita clara (~220) — um caso bimodal clássico.
        var image = CreateGray(100, 50, (x, _) => (byte)(x < 50 ? 30 : 220));

        var threshold = _sut.ComputeOtsuThreshold(image);

        Assert.InRange(threshold, 30, 220);
    }

    [Fact]
    public void Binarize_PixelsBelowThreshold_BecomeInk()
    {
        var image = CreateGray(16, 8, (x, _) => (byte)(x < 8 ? 10 : 240)); // esquerda escura, direita clara

        var result = _sut.Binarize(image, threshold: 128);

        Assert.Equal(CorePixelFormat.Bilevel1, result.Format);
        Assert.True(GetBit(result, 0, 0), "Pixel escuro deveria virar tinta (bit 1).");
        Assert.False(GetBit(result, 15, 0), "Pixel claro deveria permanecer fundo (bit 0).");
    }

    [Fact]
    public void Binarize_OutputHasCorrectDimensionsAndPackedStride()
    {
        var image = CreateGray(13, 5, (_, _) => 100); // largura não múltipla de 8, testa padding do stride

        var result = _sut.Binarize(image, threshold: 128);

        Assert.Equal(13, result.Width);
        Assert.Equal(5, result.Height);
        Assert.Equal(2, result.Stride); // ceil(13/8) = 2
    }

    [Fact]
    public void Binarize_NonGrayscaleInput_Throws()
    {
        var rgba = new RasterImage(4, 4, 16, CorePixelFormat.Rgba32, new byte[16 * 4], 200, 200);
        Assert.Throws<ArgumentException>(() => _sut.Binarize(rgba, 128));
    }

    [Fact]
    public void BinarizeAdaptive_HandlesUnevenIllumination_BetterThanGlobalThreshold()
    {
        // Fundo em gradiente (60 à esquerda subindo a 200 à direita) com uma mancha de "texto"
        // escura (valor 40) sobreposta em toda a largura. Um limiar global único não consegue
        // detectar a mancha nas duas extremidades ao mesmo tempo; o adaptativo, sim.
        const int width = 200, height = 40;
        var image = CreateGray(width, height, (x, y) =>
        {
            var background = (byte)(60 + x * (200 - 60) / width);
            var isTextBand = y is >= 15 and < 25;
            return isTextBand ? (byte)40 : background;
        });

        var adaptive = _sut.BinarizeAdaptive(image, windowSize: 31, sensitivity: 0.15);

        // A mancha de texto deve ser detectada como tinta tanto perto do fundo escuro quanto do claro.
        Assert.True(GetBit(adaptive, 10, 20), "Texto sobre fundo escuro deveria ser detectado.");
        Assert.True(GetBit(adaptive, width - 10, 20), "Texto sobre fundo claro deveria ser detectado.");

        // E o próprio fundo (fora da faixa de texto) não deveria virar tinta.
        Assert.False(GetBit(adaptive, 10, 5));
        Assert.False(GetBit(adaptive, width - 10, 5));
    }

    [Fact]
    public void ComputeOtsuThreshold_NonGrayscaleInput_Throws()
    {
        var rgba = new RasterImage(4, 4, 16, CorePixelFormat.Rgba32, new byte[16 * 4], 200, 200);
        Assert.Throws<ArgumentException>(() => _sut.ComputeOtsuThreshold(rgba));
    }
}
