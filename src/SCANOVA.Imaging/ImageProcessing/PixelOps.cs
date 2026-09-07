using SCANOVA.Core.Models;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;

namespace SCANOVA.Imaging.ImageProcessing;

/// <summary>
/// Operações de baixo nível sobre planos de pixel (extração de canal, desfoque separável),
/// compartilhadas pelos ajustes de imagem (brilho/contraste/gamma/nitidez/ruído/fundo).
/// </summary>
internal static class PixelOps
{
    public static int BytesPerPixelForChannelOps(RasterImage image) => image.Format switch
    {
        CorePixelFormat.Gray8 => 1,
        CorePixelFormat.Rgb24 => 3,
        CorePixelFormat.Rgba32 => 4,
        _ => throw new ArgumentException($"Operação não suportada para o formato {image.Format} (use Gray8, Rgb24 ou Rgba32).", nameof(image)),
    };

    /// <summary>Número de canais de COR (exclui alfa) — os canais sobre os quais os ajustes de tom operam.</summary>
    public static int ColorChannelCount(RasterImage image) => image.Format switch
    {
        CorePixelFormat.Gray8 => 1,
        CorePixelFormat.Rgb24 => 3,
        CorePixelFormat.Rgba32 => 3, // canal alfa (índice 3) nunca é alterado por ajustes de tom
        _ => throw new ArgumentException($"Operação não suportada para o formato {image.Format}.", nameof(image)),
    };

    /// <summary>Extrai um canal para um plano compacto (sem stride/padding), largura × altura bytes.</summary>
    public static byte[] ExtractChannelPlane(RasterImage image, int channelIndex)
    {
        var bpp = BytesPerPixelForChannelOps(image);
        var plane = new byte[image.Width * image.Height];
        for (var y = 0; y < image.Height; y++)
        {
            var srcRow = y * image.Stride;
            var dstRow = y * image.Width;
            for (var x = 0; x < image.Width; x++)
            {
                plane[dstRow + x] = image.Pixels[srcRow + x * bpp + channelIndex];
            }
        }

        return plane;
    }

    /// <summary>Desfoque de média separável (horizontal + vertical), O(largura × altura × raio).</summary>
    public static byte[] BoxBlurPlane(byte[] plane, int width, int height, int radius)
    {
        if (radius <= 0)
        {
            return (byte[])plane.Clone();
        }

        var horizontal = new byte[plane.Length];
        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            for (var x = 0; x < width; x++)
            {
                int sum = 0, count = 0;
                var lo = Math.Max(0, x - radius);
                var hi = Math.Min(width - 1, x + radius);
                for (var xx = lo; xx <= hi; xx++)
                {
                    sum += plane[row + xx];
                    count++;
                }

                horizontal[row + x] = (byte)(sum / count);
            }
        }

        var result = new byte[plane.Length];
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                int sum = 0, count = 0;
                var lo = Math.Max(0, y - radius);
                var hi = Math.Min(height - 1, y + radius);
                for (var yy = lo; yy <= hi; yy++)
                {
                    sum += horizontal[yy * width + x];
                    count++;
                }

                result[y * width + x] = (byte)(sum / count);
            }
        }

        return result;
    }

    /// <summary>Reconstrói um <see cref="RasterImage"/> substituindo um único canal pelo plano informado.</summary>
    public static RasterImage WithChannelReplaced(RasterImage image, int channelIndex, byte[] plane)
    {
        var bpp = BytesPerPixelForChannelOps(image);
        var pixels = (byte[])image.Pixels.Clone();
        for (var y = 0; y < image.Height; y++)
        {
            var srcRow = y * image.Width;
            var dstRow = y * image.Stride;
            for (var x = 0; x < image.Width; x++)
            {
                pixels[dstRow + x * bpp + channelIndex] = plane[srcRow + x];
            }
        }

        return new RasterImage(image.Width, image.Height, image.Stride, image.Format, pixels, image.HorizontalDpi, image.VerticalDpi);
    }
}
