using SCANOVA.Core.Models;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;

namespace SCANOVA.Imaging.Tests;

/// <summary>Gera imagens sintéticas para teste (nunca documentos reais — seção 122 da especificação).</summary>
internal static class TestImages
{
    /// <summary>
    /// Cria uma imagem RGBA sintética contendo um "documento" retangular claro sobre um fundo
    /// escuro, com algumas linhas horizontais escuras simulando texto.
    /// </summary>
    public static RasterImage CreateDocumentLike(int width = 400, int height = 300)
    {
        var stride = width * 4;
        var pixels = new byte[stride * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = y * stride + x * 4;

                var isBackground = x < 20 || x >= width - 20 || y < 20 || y >= height - 20;
                var isTextLine = !isBackground && (y % 30 < 4) && x > 40 && x < width - 40;

                byte value = isBackground ? (byte)60 : isTextLine ? (byte)20 : (byte)250;

                pixels[offset] = value;
                pixels[offset + 1] = value;
                pixels[offset + 2] = value;
                pixels[offset + 3] = 255;
            }
        }

        return new RasterImage(width, height, stride, CorePixelFormat.Rgba32, pixels, 200, 200);
    }

    public static RasterImage CreateSolid(int width, int height, byte r, byte g, byte b)
    {
        var stride = width * 4;
        var pixels = new byte[stride * height];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = r;
            pixels[i + 1] = g;
            pixels[i + 2] = b;
            pixels[i + 3] = 255;
        }

        return new RasterImage(width, height, stride, CorePixelFormat.Rgba32, pixels, 200, 200);
    }
}
