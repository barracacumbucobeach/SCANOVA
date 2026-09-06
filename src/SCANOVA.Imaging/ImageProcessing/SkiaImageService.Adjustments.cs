using SCANOVA.Core.Models;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;

namespace SCANOVA.Imaging.ImageProcessing;

/// <summary>
/// Ajustes de tom/nitidez/ruído (seção 19/44) — funções puras sobre o buffer de pixels, sem
/// dependência do SkiaSharp. Operam nos canais de cor (Gray8/Rgb24/Rgba32); o canal alfa, quando
/// presente, nunca é alterado.
/// </summary>
public sealed partial class SkiaImageService
{
    public RasterImage AdjustBrightnessContrast(RasterImage image, int brightness, int contrast)
    {
        var b = Math.Clamp(brightness, -100, 100);
        // A fórmula clássica de contraste é definida para c em [-255, 255]; reescala nosso
        // intervalo de UI [-100, 100] para esse domínio antes de aplicar.
        var scaledContrast = Math.Clamp(contrast, -100, 100) * 255.0 / 100.0;
        var factor = (259.0 * (scaledContrast + 255)) / (255.0 * (259 - scaledContrast));

        return MapColorChannels(image, v =>
        {
            var value = factor * (v - 128) + 128 + b;
            return (byte)Math.Clamp(Math.Round(value), 0, 255);
        });
    }

    public RasterImage AdjustGamma(RasterImage image, double gamma)
    {
        if (gamma <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gamma), "Gamma deve ser maior que zero.");
        }

        var invGamma = 1.0 / gamma;

        // Tabela de 256 valores — mais barato que recalcular Math.Pow por pixel.
        var lut = new byte[256];
        for (var i = 0; i < 256; i++)
        {
            lut[i] = (byte)Math.Clamp(Math.Round(255.0 * Math.Pow(i / 255.0, invGamma)), 0, 255);
        }

        return MapColorChannels(image, v => lut[v]);
    }

    public RasterImage Sharpen(RasterImage image, double amount)
    {
        if (amount <= 0)
        {
            return image;
        }

        var channels = PixelOps.ColorChannelCount(image);
        var result = image;
        for (var c = 0; c < channels; c++)
        {
            var original = PixelOps.ExtractChannelPlane(result, c);
            var blurred = PixelOps.BoxBlurPlane(original, image.Width, image.Height, radius: 2);

            var sharpened = new byte[original.Length];
            for (var i = 0; i < original.Length; i++)
            {
                // Máscara de nitidez: original + amount × (original − desfocado).
                var value = original[i] + amount * (original[i] - blurred[i]);
                sharpened[i] = (byte)Math.Clamp(Math.Round(value), 0, 255);
            }

            result = PixelOps.WithChannelReplaced(result, c, sharpened);
        }

        return result;
    }

    public RasterImage ReduceNoise(RasterImage image, int radius = 1)
    {
        if (radius <= 0)
        {
            return image;
        }

        var channels = PixelOps.ColorChannelCount(image);
        var result = image;
        for (var c = 0; c < channels; c++)
        {
            var plane = PixelOps.ExtractChannelPlane(result, c);
            var blurred = PixelOps.BoxBlurPlane(plane, image.Width, image.Height, radius);
            result = PixelOps.WithChannelReplaced(result, c, blurred);
        }

        return result;
    }

    public RasterImage RemoveBackground(RasterImage grayscaleImage, int blurRadius = 15)
    {
        if (grayscaleImage.Format != CorePixelFormat.Gray8)
        {
            throw new ArgumentException($"RemoveBackground requer uma imagem {CorePixelFormat.Gray8}, recebido {grayscaleImage.Format}.", nameof(grayscaleImage));
        }

        var original = PixelOps.ExtractChannelPlane(grayscaleImage, 0);
        // O fundo estimado é a versão fortemente desfocada da imagem — captura a
        // iluminação/sombra de baixa frequência, sem o texto/tinta de alta frequência.
        var background = PixelOps.BoxBlurPlane(original, grayscaleImage.Width, grayscaleImage.Height, blurRadius);

        var corrected = new byte[original.Length];
        for (var i = 0; i < original.Length; i++)
        {
            // Normaliza para que o fundo estimado vire branco (255); preserva o contraste do
            // primeiro plano em relação a ele.
            var value = 255.0 - (background[i] - original[i]);
            corrected[i] = (byte)Math.Clamp(Math.Round(value), 0, 255);
        }

        return PixelOps.WithChannelReplaced(grayscaleImage, 0, corrected);
    }

    public RasterImage AdjustSaturation(RasterImage image, int saturation)
    {
        if (image.Format is CorePixelFormat.Gray8 or CorePixelFormat.Bilevel1)
        {
            // Saturação não se aplica fora do modo colorido (seção 19) — nenhuma alteração.
            return image;
        }

        var s = Math.Clamp(saturation, -100, 100);
        if (s == 0)
        {
            return image;
        }

        var factor = 1.0 + s / 100.0;
        var bpp = PixelOps.BytesPerPixelForChannelOps(image);
        var pixels = (byte[])image.Pixels.Clone();

        for (var y = 0; y < image.Height; y++)
        {
            var row = y * image.Stride;
            for (var x = 0; x < image.Width; x++)
            {
                var offset = row + x * bpp;
                var r = pixels[offset];
                var g = pixels[offset + 1];
                var b = pixels[offset + 2];
                var luminance = 0.299 * r + 0.587 * g + 0.114 * b;

                pixels[offset] = (byte)Math.Clamp(Math.Round(luminance + (r - luminance) * factor), 0, 255);
                pixels[offset + 1] = (byte)Math.Clamp(Math.Round(luminance + (g - luminance) * factor), 0, 255);
                pixels[offset + 2] = (byte)Math.Clamp(Math.Round(luminance + (b - luminance) * factor), 0, 255);
            }
        }

        return new RasterImage(image.Width, image.Height, image.Stride, image.Format, pixels, image.HorizontalDpi, image.VerticalDpi);
    }

    private static RasterImage MapColorChannels(RasterImage image, Func<byte, byte> transform)
    {
        var channels = PixelOps.ColorChannelCount(image);
        var bpp = PixelOps.BytesPerPixelForChannelOps(image);
        var pixels = (byte[])image.Pixels.Clone();

        for (var y = 0; y < image.Height; y++)
        {
            var row = y * image.Stride;
            for (var x = 0; x < image.Width; x++)
            {
                var pixelOffset = row + x * bpp;
                for (var c = 0; c < channels; c++)
                {
                    pixels[pixelOffset + c] = transform(pixels[pixelOffset + c]);
                }
            }
        }

        return new RasterImage(image.Width, image.Height, image.Stride, image.Format, pixels, image.HorizontalDpi, image.VerticalDpi);
    }
}
