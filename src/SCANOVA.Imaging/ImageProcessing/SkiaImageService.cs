using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SkiaSharp;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;

namespace SCANOVA.Imaging.ImageProcessing;

/// <summary>
/// Implementação de <see cref="IImageService"/> usando SkiaSharp. Os métodos de binarização
/// ficam em <c>Binarization/SkiaImageService.Binarization.cs</c> (mesma classe, arquivo
/// separado) — são algoritmos puros sobre o buffer de pixels, sem dependência do SkiaSharp.
/// </summary>
public sealed partial class SkiaImageService : IImageService
{
    public RasterImage Rotate(RasterImage image, int degrees)
    {
        var normalized = ((degrees % 360) + 360) % 360;
        if (normalized == 0)
        {
            return image;
        }

        using var src = SkiaConversions.ToSkBitmap(image);

        var radians = normalized * Math.PI / 180.0;
        var sin = Math.Abs(Math.Sin(radians));
        var cos = Math.Abs(Math.Cos(radians));
        var newWidth = Math.Max(1, (int)Math.Round(image.Width * cos + image.Height * sin));
        var newHeight = Math.Max(1, (int)Math.Round(image.Width * sin + image.Height * cos));

        return RenderTransformed(src, newWidth, newHeight, canvas =>
        {
            canvas.Translate(newWidth / 2f, newHeight / 2f);
            canvas.RotateDegrees(normalized);
            canvas.Translate(-image.Width / 2f, -image.Height / 2f);
        }, image.HorizontalDpi, image.VerticalDpi);
    }

    public RasterImage FlipHorizontal(RasterImage image)
    {
        using var src = SkiaConversions.ToSkBitmap(image);
        return RenderTransformed(src, image.Width, image.Height, canvas =>
        {
            canvas.Translate(image.Width, 0);
            canvas.Scale(-1, 1);
        }, image.HorizontalDpi, image.VerticalDpi);
    }

    public RasterImage FlipVertical(RasterImage image)
    {
        using var src = SkiaConversions.ToSkBitmap(image);
        return RenderTransformed(src, image.Width, image.Height, canvas =>
        {
            canvas.Translate(0, image.Height);
            canvas.Scale(1, -1);
        }, image.HorizontalDpi, image.VerticalDpi);
    }

    public RasterImage Crop(RasterImage image, CropRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);

        var minX = Min4(region.TopLeft.X, region.TopRight.X, region.BottomRight.X, region.BottomLeft.X);
        var maxX = Max4(region.TopLeft.X, region.TopRight.X, region.BottomRight.X, region.BottomLeft.X);
        var minY = Min4(region.TopLeft.Y, region.TopRight.Y, region.BottomRight.Y, region.BottomLeft.Y);
        var maxY = Max4(region.TopLeft.Y, region.TopRight.Y, region.BottomRight.Y, region.BottomLeft.Y);

        var x = Math.Max(0, (int)Math.Round(minX));
        var y = Math.Max(0, (int)Math.Round(minY));
        var right = Math.Min(image.Width, (int)Math.Round(maxX));
        var bottom = Math.Min(image.Height, (int)Math.Round(maxY));
        var width = Math.Max(1, right - x);
        var height = Math.Max(1, bottom - y);

        using var src = SkiaConversions.ToSkBitmap(image);
        using var subset = new SKBitmap();
        var region2 = new SKRectI(x, y, x + width, y + height);

        if (!src.ExtractSubset(subset, region2))
        {
            throw new InvalidOperationException("Não foi possível cortar a imagem com a região informada.");
        }

        return SkiaConversions.ToRasterImage(subset, image.HorizontalDpi, image.VerticalDpi);
    }

    public RasterImage ToGrayscale(RasterImage image)
    {
        if (image.Format == CorePixelFormat.Gray8)
        {
            return image;
        }

        var rgba = SkiaConversions.ToRgba32Bytes(image);
        var gray = new byte[image.Width * image.Height];

        for (int i = 0, p = 0; i < gray.Length; i++, p += 4)
        {
            // Luminância perceptual (Rec. 601), consistente com conversões de escala de cinza em ferramentas de imagem comuns.
            var r = rgba[p];
            var g = rgba[p + 1];
            var b = rgba[p + 2];
            gray[i] = (byte)Math.Round(0.299 * r + 0.587 * g + 0.114 * b);
        }

        return new RasterImage(image.Width, image.Height, image.Width, CorePixelFormat.Gray8, gray, image.HorizontalDpi, image.VerticalDpi);
    }

    public RasterImage NormalizeDpi(RasterImage image, double targetDpi)
    {
        if (targetDpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetDpi));
        }

        if (Math.Abs(image.HorizontalDpi - targetDpi) < 0.01 && Math.Abs(image.VerticalDpi - targetDpi) < 0.01)
        {
            return image;
        }

        var scaleX = image.HorizontalDpi > 0 ? targetDpi / image.HorizontalDpi : 1.0;
        var scaleY = image.VerticalDpi > 0 ? targetDpi / image.VerticalDpi : 1.0;

        var newWidth = Math.Max(1, (int)Math.Round(image.Width * scaleX));
        var newHeight = Math.Max(1, (int)Math.Round(image.Height * scaleY));

        using var src = SkiaConversions.ToSkBitmap(image);
        using var resized = src.Resize(new SKImageInfo(newWidth, newHeight, SKColorType.Rgba8888, SKAlphaType.Unpremul), SKFilterQuality.High);

        if (resized is null)
        {
            throw new InvalidOperationException("Não foi possível redimensionar a imagem para o DPI solicitado.");
        }

        var result = SkiaConversions.ToRasterImage(resized, targetDpi, targetDpi);

        // O redimensionamento sempre passa por RGBA (para usar filtragem de alta qualidade);
        // restaura o formato de pixel original quando ele não é RGBA, para que NormalizeDpi
        // nunca corrompa silenciosamente o formato de uma imagem em escala de cinza ou bilevel.
        return image.Format switch
        {
            CorePixelFormat.Gray8 => ToGrayscale(result),
            // Reamostra como cinza (com suavização) e rebinariza no ponto médio: melhor
            // qualidade do que redimensionar o bitmap 1-bit diretamente teria (que produziria
            // serrilhado sem essa etapa intermediária em escala de cinza).
            CorePixelFormat.Bilevel1 => Binarize(ToGrayscale(result), 128),
            _ => result,
        };
    }

    private static RasterImage RenderTransformed(SKBitmap src, int width, int height, Action<SKCanvas> configureCanvas, double hDpi, double vDpi)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        canvas.Save();
        configureCanvas(canvas);
        canvas.DrawBitmap(src, 0, 0);
        canvas.Restore();
        canvas.Flush();

        using var snapshot = surface.Snapshot();
        using var resultBitmap = SKBitmap.FromImage(snapshot);
        return SkiaConversions.ToRasterImage(resultBitmap, hDpi, vDpi);
    }

    private static double Min4(double a, double b, double c, double d) => Math.Min(Math.Min(a, b), Math.Min(c, d));
    private static double Max4(double a, double b, double c, double d) => Math.Max(Math.Max(a, b), Math.Max(c, d));
}
