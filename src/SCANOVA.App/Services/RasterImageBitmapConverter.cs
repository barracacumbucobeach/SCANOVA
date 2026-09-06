using Microsoft.UI.Xaml.Media.Imaging;
using SCANOVA.Core.Models;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;

namespace SCANOVA.App.Services;

/// <summary>
/// Converte um <see cref="RasterImage"/> (RGBA32, independente de biblioteca) em um
/// <see cref="WriteableBitmap"/> (BGRA8, formato de pixel do WinUI 3) para exibição em um
/// controle <c>Image</c>. Fica em <c>SCANOVA.App</c> — e não em <c>SCANOVA.Imaging</c> — porque
/// é a única camada com dependência de WinUI.
/// </summary>
public static class RasterImageBitmapConverter
{
    public static async Task<WriteableBitmap> ToWriteableBitmapAsync(RasterImage image)
    {
        if (image.Format != CorePixelFormat.Rgba32)
        {
            throw new ArgumentException($"Conversão para exibição requer {CorePixelFormat.Rgba32}, recebido {image.Format}.", nameof(image));
        }

        // A troca de canais (RGBA -> BGRA) é pura CPU e não toca UI — roda no thread pool
        // (seção 7/61). Só a construção do WriteableBitmap em si precisa da UI thread.
        var bgra = await Task.Run(() => ConvertRgbaToBgra(image));

        var bitmap = new WriteableBitmap(image.Width, image.Height);
        using (var stream = bitmap.PixelBuffer.AsStream())
        {
            await stream.WriteAsync(bgra);
        }

        bitmap.Invalidate();
        return bitmap;
    }

    private static byte[] ConvertRgbaToBgra(RasterImage image)
    {
        var bgra = new byte[image.Width * image.Height * 4];

        for (var y = 0; y < image.Height; y++)
        {
            var srcRow = y * image.Stride;
            var dstRow = y * image.Width * 4;
            for (var x = 0; x < image.Width; x++)
            {
                var s = srcRow + x * 4;
                var d = dstRow + x * 4;
                bgra[d] = image.Pixels[s + 2];     // B
                bgra[d + 1] = image.Pixels[s + 1]; // G
                bgra[d + 2] = image.Pixels[s];     // R
                bgra[d + 3] = image.Pixels[s + 3]; // A
            }
        }

        return bgra;
    }
}
