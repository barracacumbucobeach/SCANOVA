using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SCANOVA.Imaging.ImageProcessing;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;
using Xunit;

namespace SCANOVA.Imaging.Tests;

public class AdjustmentsTests
{
    private readonly IImageService _sut = new SkiaImageService();

    private static RasterImage CreateGray(int width, int height, byte value)
    {
        var pixels = new byte[width * height];
        Array.Fill(pixels, value);
        return new RasterImage(width, height, width, CorePixelFormat.Gray8, pixels, 200, 200);
    }

    [Fact]
    public void AdjustBrightnessContrast_PositiveBrightness_LightensImage()
    {
        var image = CreateGray(10, 10, 100);

        var result = _sut.AdjustBrightnessContrast(image, brightness: 50, contrast: 0);

        Assert.Equal(150, result.Pixels[0]);
    }

    [Fact]
    public void AdjustBrightnessContrast_ZeroZero_IsNoOp()
    {
        var image = CreateGray(10, 10, 77);

        var result = _sut.AdjustBrightnessContrast(image, 0, 0);

        Assert.Equal(77, result.Pixels[0]);
    }

    [Fact]
    public void AdjustBrightnessContrast_ClampsAtBounds()
    {
        var image = CreateGray(4, 4, 250);

        var result = _sut.AdjustBrightnessContrast(image, brightness: 100, contrast: 0);

        Assert.All(result.Pixels, p => Assert.Equal(255, p));
    }

    [Fact]
    public void AdjustGamma_LessThanOne_DarkensMidtones()
    {
        var image = CreateGray(4, 4, 128);

        var result = _sut.AdjustGamma(image, 0.5);

        Assert.True(result.Pixels[0] < 128);
    }

    [Fact]
    public void AdjustGamma_GreaterThanOne_LightensMidtones()
    {
        var image = CreateGray(4, 4, 128);

        var result = _sut.AdjustGamma(image, 2.0);

        Assert.True(result.Pixels[0] > 128);
    }

    [Fact]
    public void AdjustGamma_One_IsApproximatelyNoOp()
    {
        var image = CreateGray(4, 4, 128);

        var result = _sut.AdjustGamma(image, 1.0);

        Assert.InRange(result.Pixels[0], 127, 129);
    }

    [Fact]
    public void Sharpen_IncreasesLocalContrastAtEdge()
    {
        // Metade escura, metade clara — uma borda nítida.
        const int width = 20, height = 10;
        var pixels = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                pixels[y * width + x] = (byte)(x < width / 2 ? 80 : 180);
            }
        }
        var image = new RasterImage(width, height, width, CorePixelFormat.Gray8, pixels, 200, 200);

        var result = _sut.Sharpen(image, amount: 1.0);

        // Logo à esquerda da borda, o lado escuro deve ficar ainda mais escuro (overshoot da máscara de nitidez).
        var beforeEdge = image.Pixels[5 * width + (width / 2 - 1)];
        var afterSharpen = result.Pixels[5 * width + (width / 2 - 1)];
        Assert.True(afterSharpen <= beforeEdge);
    }

    [Fact]
    public void ReduceNoise_SmoothsSaltAndPepperNoise()
    {
        const int size = 30;
        var pixels = new byte[size * size];
        Array.Fill(pixels, (byte)128);
        // Injeta ruído extremo em um único pixel central.
        pixels[15 * size + 15] = 255;
        var image = new RasterImage(size, size, size, CorePixelFormat.Gray8, pixels, 200, 200);

        var result = _sut.ReduceNoise(image, radius: 2);

        Assert.True(result.Pixels[15 * size + 15] < 255);
        Assert.True(result.Pixels[15 * size + 15] > 128);
    }

    [Fact]
    public void RemoveBackground_FlattensSmoothGradientToWhite()
    {
        // Fundo puramente um gradiente suave de iluminação (60→200), sem nenhum traço de tinta.
        // Como o "fundo estimado" (desfoque grande) é essencialmente igual ao valor original em
        // toda a imagem, a correção deve levar praticamente todo pixel para perto do branco (255) —
        // é exatamente essa normalização de iluminação desigual que a função existe para fazer.
        const int width = 100, height = 20;
        var pixels = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                pixels[y * width + x] = (byte)(60 + x * (200 - 60) / width);
            }
        }
        var image = new RasterImage(width, height, width, CorePixelFormat.Gray8, pixels, 200, 200);

        var result = _sut.RemoveBackground(image, blurRadius: 15);

        Assert.All(result.Pixels, p => Assert.True(p >= 240, $"Esperava um pixel de fundo próximo do branco, obtido {p}."));
    }

    [Fact]
    public void RemoveBackground_EqualizesRelativeInkDarknessAcrossUnevenBackground()
    {
        // Gradiente de fundo (60→200) com um pequeno ponto de "tinta" isolado (3×3, bem menor que
        // o raio de desfoque) embutido em cada extremidade do gradiente. A tinta é definida como um
        // valor fixo ABAIXO do fundo local (não um valor absoluto fixo) — o cenário realista de um
        // traço de caneta/impressão que absorve uma quantidade de luz relativamente constante,
        // independente de a página estar mal iluminada naquele ponto. Como o ponto é pequeno em
        // relação ao raio, o "fundo estimado" ali continua próximo do fundo real local, então a
        // correção deve equalizar os dois pontos para valores finais próximos, mesmo partindo de
        // fundos originais bem diferentes (74 vs. 186) — essa equalização é o propósito da função.
        const int width = 100, height = 20;
        const int dropBelowBackground = 60;
        var pixels = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                pixels[y * width + x] = (byte)(60 + x * (200 - 60) / width);
            }
        }

        void PaintDot(int cx, int cy)
        {
            var localBackground = 60 + cx * (200 - 60) / width;
            var inkValue = (byte)Math.Clamp(localBackground - dropBelowBackground, 0, 255);
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    pixels[(cy + dy) * width + (cx + dx)] = inkValue;
                }
            }
        }

        PaintDot(10, 10);
        PaintDot(90, 10);

        var image = new RasterImage(width, height, width, CorePixelFormat.Gray8, pixels, 200, 200);

        var result = _sut.RemoveBackground(image, blurRadius: 15);

        var dotNearDarkBackground = result.Pixels[10 * width + 10];
        var dotNearLightBackground = result.Pixels[10 * width + 90];

        Assert.True(Math.Abs(dotNearDarkBackground - dotNearLightBackground) < 30);
    }

    [Fact]
    public void RemoveBackground_NonGrayscaleInput_Throws()
    {
        var rgba = new RasterImage(4, 4, 16, CorePixelFormat.Rgba32, new byte[16 * 4], 200, 200);
        Assert.Throws<ArgumentException>(() => _sut.RemoveBackground(rgba));
    }

    private static RasterImage CreateRgba(int width, int height, byte r, byte g, byte b)
    {
        var stride = width * 4;
        var pixels = new byte[stride * height];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = r; pixels[i + 1] = g; pixels[i + 2] = b; pixels[i + 3] = 255;
        }

        return new RasterImage(width, height, stride, CorePixelFormat.Rgba32, pixels, 200, 200);
    }

    [Fact]
    public void AdjustSaturation_MinusOneHundred_ProducesGrayPixel()
    {
        var image = CreateRgba(4, 4, 200, 50, 50);

        var result = _sut.AdjustSaturation(image, -100);

        Assert.Equal(result.Pixels[0], result.Pixels[1]);
        Assert.Equal(result.Pixels[1], result.Pixels[2]);
    }

    [Fact]
    public void AdjustSaturation_Zero_IsNoOp()
    {
        var image = CreateRgba(4, 4, 200, 50, 50);

        var result = _sut.AdjustSaturation(image, 0);

        Assert.Equal(image.Pixels, result.Pixels);
    }

    [Fact]
    public void AdjustSaturation_Positive_IncreasesChannelSpread()
    {
        var image = CreateRgba(4, 4, 200, 50, 50);

        var result = _sut.AdjustSaturation(image, 50);

        var originalSpread = Math.Abs(image.Pixels[0] - image.Pixels[1]);
        var resultSpread = Math.Abs(result.Pixels[0] - result.Pixels[1]);
        Assert.True(resultSpread > originalSpread);
    }

    [Fact]
    public void AdjustSaturation_GrayscaleInput_ReturnsSameInstance()
    {
        var image = CreateGray(4, 4, 128);

        var result = _sut.AdjustSaturation(image, 50);

        Assert.Same(image, result);
    }
}
