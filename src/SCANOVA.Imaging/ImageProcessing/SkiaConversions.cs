using SCANOVA.Core.Models;
using SkiaSharp;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;

namespace SCANOVA.Imaging.ImageProcessing;

/// <summary>Conversões entre <see cref="RasterImage"/> (independente de biblioteca) e os tipos do SkiaSharp.</summary>
internal static class SkiaConversions
{
    /// <summary>
    /// Converte um <see cref="RasterImage"/> em um <see cref="SKBitmap"/> Rgba8888, expandindo
    /// formatos de menor profundidade (Gray8, Rgb24, Bilevel1) quando necessário. O chamador é
    /// responsável por descartar o bitmap retornado.
    /// </summary>
    public static SKBitmap ToSkBitmap(RasterImage image)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var bitmap = new SKBitmap(info);

        var rgba = ToRgba32Bytes(image);
        System.Runtime.InteropServices.Marshal.Copy(rgba, 0, bitmap.GetPixels(), rgba.Length);
        return bitmap;
    }

    /// <summary>Converte o buffer de pixels de um <see cref="RasterImage"/> para RGBA32 (linear, sem stride extra).</summary>
    public static byte[] ToRgba32Bytes(RasterImage image)
    {
        var output = new byte[image.Width * image.Height * 4];

        switch (image.Format)
        {
            case CorePixelFormat.Rgba32:
                CopyRowsToTightBuffer(image, output, 4);
                return output;

            case CorePixelFormat.Rgb24:
                {
                    for (var y = 0; y < image.Height; y++)
                    {
                        var srcRow = y * image.Stride;
                        var dstRow = y * image.Width * 4;
                        for (var x = 0; x < image.Width; x++)
                        {
                            var s = srcRow + x * 3;
                            var d = dstRow + x * 4;
                            output[d] = image.Pixels[s];
                            output[d + 1] = image.Pixels[s + 1];
                            output[d + 2] = image.Pixels[s + 2];
                            output[d + 3] = 255;
                        }
                    }
                    return output;
                }

            case CorePixelFormat.Gray8:
                {
                    for (var y = 0; y < image.Height; y++)
                    {
                        var srcRow = y * image.Stride;
                        var dstRow = y * image.Width * 4;
                        for (var x = 0; x < image.Width; x++)
                        {
                            var g = image.Pixels[srcRow + x];
                            var d = dstRow + x * 4;
                            output[d] = g;
                            output[d + 1] = g;
                            output[d + 2] = g;
                            output[d + 3] = 255;
                        }
                    }
                    return output;
                }

            case CorePixelFormat.Bilevel1:
                {
                    for (var y = 0; y < image.Height; y++)
                    {
                        var srcRow = y * image.Stride;
                        var dstRow = y * image.Width * 4;
                        for (var x = 0; x < image.Width; x++)
                        {
                            var bit = (image.Pixels[srcRow + x / 8] >> (7 - x % 8)) & 1;
                            byte g = bit == 1 ? (byte)0 : (byte)255; // bit 1 = preto
                            var d = dstRow + x * 4;
                            output[d] = g;
                            output[d + 1] = g;
                            output[d + 2] = g;
                            output[d + 3] = 255;
                        }
                    }
                    return output;
                }

            default:
                throw new NotSupportedException($"Formato de pixel não suportado: {image.Format}");
        }
    }

    private static void CopyRowsToTightBuffer(RasterImage image, byte[] output, int bytesPerPixel)
    {
        var tightStride = image.Width * bytesPerPixel;
        for (var y = 0; y < image.Height; y++)
        {
            Buffer.BlockCopy(image.Pixels, y * image.Stride, output, y * tightStride, tightStride);
        }
    }

    /// <summary>Converte um <see cref="SKBitmap"/> Rgba8888 de volta para um <see cref="RasterImage"/> Rgba32.</summary>
    public static RasterImage ToRasterImage(SKBitmap bitmap, double horizontalDpi, double verticalDpi)
    {
        using var rgba = bitmap.ColorType == SKColorType.Rgba8888 ? null : bitmap.Copy(SKColorType.Rgba8888);
        var source = rgba ?? bitmap;

        var stride = source.RowBytes;
        var pixels = new byte[stride * source.Height];
        System.Runtime.InteropServices.Marshal.Copy(source.GetPixels(), pixels, 0, pixels.Length);

        return new RasterImage(source.Width, source.Height, stride, CorePixelFormat.Rgba32, pixels, horizontalDpi, verticalDpi);
    }
}
