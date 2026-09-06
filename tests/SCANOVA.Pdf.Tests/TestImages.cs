using SCANOVA.Core.Models;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;

namespace SCANOVA.Pdf.Tests;

/// <summary>Gera imagens sintéticas para teste (nunca documentos reais — seção 122 da especificação).</summary>
internal static class TestImages
{
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

    public static RasterImage CreateGray(int width, int height, double dpi = 200, byte value = 128)
    {
        var pixels = new byte[width * height];
        Array.Fill(pixels, value);
        return new RasterImage(width, height, width, CorePixelFormat.Gray8, pixels, dpi, dpi);
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
