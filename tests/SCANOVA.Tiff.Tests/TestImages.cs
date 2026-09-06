using SCANOVA.Core.Models;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;

namespace SCANOVA.Tiff.Tests;

/// <summary>Gera imagens sintéticas para teste (nunca documentos reais — seção 122 da especificação).</summary>
internal static class TestImages
{
    /// <summary>Imagem RGBA sintética: fundo claro com uma "moldura" e "linhas de texto" escuras, como um documento escaneado.</summary>
    public static RasterImage CreateDocumentLike(int width = 400, int height = 300, double dpi = 200)
    {
        var stride = width * 4;
        var pixels = new byte[stride * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = y * stride + x * 4;
                var isBorder = x < 20 || x >= width - 20 || y < 20 || y >= height - 20;
                var isTextLine = !isBorder && (y % 30 < 4) && x > 40 && x < width - 40;
                byte value = isBorder ? (byte)210 : isTextLine ? (byte)20 : (byte)250;

                pixels[offset] = value;
                pixels[offset + 1] = value;
                pixels[offset + 2] = value;
                pixels[offset + 3] = 255;
            }
        }

        return new RasterImage(width, height, stride, CorePixelFormat.Rgba32, pixels, dpi, dpi);
    }

    public static RasterImage CreateGray(int width, int height, Func<int, int, byte> pixelValue, double dpi = 200)
    {
        var pixels = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                pixels[y * width + x] = pixelValue(x, y);
            }
        }

        return new RasterImage(width, height, width, CorePixelFormat.Gray8, pixels, dpi, dpi);
    }

    /// <summary>Imagem bilevel sintética: um retângulo de "tinta" (bit 1) centralizado sobre fundo branco (bit 0).</summary>
    public static RasterImage CreateBilevel(int width, int height, double dpi = 200)
    {
        var stride = RasterImage.MinimumStride(width, CorePixelFormat.Bilevel1);
        var pixels = new byte[stride * height];

        var rectLeft = width / 4;
        var rectRight = width - width / 4;
        var rectTop = height / 4;
        var rectBottom = height - height / 4;

        for (var y = rectTop; y < rectBottom; y++)
        {
            for (var x = rectLeft; x < rectRight; x++)
            {
                pixels[y * stride + (x >> 3)] |= (byte)(0x80 >> (x & 7));
            }
        }

        return new RasterImage(width, height, stride, CorePixelFormat.Bilevel1, pixels, dpi, dpi);
    }

    public static RasterImage CreateRgb24(int width, int height, double dpi = 200)
    {
        var stride = width * 3;
        var pixels = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = y * stride + x * 3;
                pixels[offset] = (byte)(x % 256);
                pixels[offset + 1] = (byte)(y % 256);
                pixels[offset + 2] = 128;
            }
        }

        return new RasterImage(width, height, stride, CorePixelFormat.Rgb24, pixels, dpi, dpi);
    }
}
