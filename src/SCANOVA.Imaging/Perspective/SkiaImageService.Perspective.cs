using SCANOVA.Core.Models;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;

namespace SCANOVA.Imaging.ImageProcessing;

/// <summary>
/// Correção de perspectiva (seção 17): mapeia um quadrilátero arbitrário (documento fotografado
/// em ângulo) para um retângulo alinhado aos eixos, via homografia clássica de "unit square para
/// quadrilátero" (Heckbert, "Fundamentals of Texture Mapping and Image Warping", 1989 — a
/// derivação padrão e amplamente usada para este problema).
/// </summary>
public sealed partial class SkiaImageService
{
    public RasterImage CorrectPerspective(RasterImage image, CropRegion quad, int outputWidth, int outputHeight)
    {
        ArgumentNullException.ThrowIfNull(quad);
        if (outputWidth <= 0 || outputHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(outputWidth <= 0 ? nameof(outputWidth) : nameof(outputHeight));
        }

        // Sempre passa por RGBA para o warp — a correção de perspectiva acontece cedo no
        // pipeline (seção 84/85), antes da conversão para cinza/binarização.
        var sourceRgba = SkiaConversions.ToRgba32Bytes(image);
        var srcWidth = image.Width;
        var srcHeight = image.Height;

        var (a, b, c, d, e, f, g, h) = ComputeUnitSquareToQuadCoefficients(quad);

        var destStride = outputWidth * 4;
        var destPixels = new byte[destStride * outputHeight];

        for (var py = 0; py < outputHeight; py++)
        {
            var v = (py + 0.5) / outputHeight;
            var destRow = py * destStride;

            for (var px = 0; px < outputWidth; px++)
            {
                var u = (px + 0.5) / outputWidth;

                var denom = g * u + h * v + 1.0;
                var sx = (a * u + b * v + c) / denom;
                var sy = (d * u + e * v + f) / denom;

                var destOffset = destRow + px * 4;
                SampleBilinear(sourceRgba, srcWidth, srcHeight, sx, sy, destPixels, destOffset);
            }
        }

        return new RasterImage(outputWidth, outputHeight, destStride, CorePixelFormat.Rgba32, destPixels, image.HorizontalDpi, image.VerticalDpi);
    }

    /// <summary>
    /// Coeficientes da transformação projetiva que mapeia o quadrado unitário [0,1]×[0,1] para o
    /// quadrilátero <paramref name="quad"/>: TopLeft→(0,0), TopRight→(1,0), BottomRight→(1,1),
    /// BottomLeft→(0,1). Ponto (u,v) → ((a·u+b·v+c)/(g·u+h·v+1), (d·u+e·v+f)/(g·u+h·v+1)).
    /// </summary>
    private static (double a, double b, double c, double d, double e, double f, double g, double h) ComputeUnitSquareToQuadCoefficients(CropRegion quad)
    {
        var x0 = quad.TopLeft.X;
        var y0 = quad.TopLeft.Y;
        var x1 = quad.TopRight.X;
        var y1 = quad.TopRight.Y;
        var x2 = quad.BottomRight.X;
        var y2 = quad.BottomRight.Y;
        var x3 = quad.BottomLeft.X;
        var y3 = quad.BottomLeft.Y;

        var dx1 = x1 - x2;
        var dx2 = x3 - x2;
        var dx3 = x0 - x1 + x2 - x3;
        var dy1 = y1 - y2;
        var dy2 = y3 - y2;
        var dy3 = y0 - y1 + y2 - y3;

        double g, h;
        if (Math.Abs(dx3) < 1e-9 && Math.Abs(dy3) < 1e-9)
        {
            // Já é um paralelogramo (caso afim) — sem termo projetivo.
            g = 0;
            h = 0;
        }
        else
        {
            var denom = dx1 * dy2 - dx2 * dy1;
            if (Math.Abs(denom) < 1e-12)
            {
                // Quadrilátero degenerado (pontos colineares) — cai para afim em vez de dividir por ~0.
                g = 0;
                h = 0;
            }
            else
            {
                g = (dx3 * dy2 - dx2 * dy3) / denom;
                h = (dx1 * dy3 - dx3 * dy1) / denom;
            }
        }

        var a = x1 - x0 + g * x1;
        var b = x3 - x0 + h * x3;
        var c = x0;
        var d = y1 - y0 + g * y1;
        var e = y3 - y0 + h * y3;
        var f = y0;

        return (a, b, c, d, e, f, g, h);
    }

    private static void SampleBilinear(byte[] sourceRgba, int srcWidth, int srcHeight, double sx, double sy, byte[] dest, int destOffset)
    {
        // Fora dos limites da imagem de origem: preenche com branco (fundo neutro esperado
        // fora do documento detectado, em vez de lixo/preto).
        if (sx < 0 || sy < 0 || sx > srcWidth - 1 || sy > srcHeight - 1)
        {
            dest[destOffset] = 255;
            dest[destOffset + 1] = 255;
            dest[destOffset + 2] = 255;
            dest[destOffset + 3] = 255;
            return;
        }

        var x0 = (int)Math.Floor(sx);
        var y0 = (int)Math.Floor(sy);
        var x1 = Math.Min(x0 + 1, srcWidth - 1);
        var y1 = Math.Min(y0 + 1, srcHeight - 1);
        var fx = sx - x0;
        var fy = sy - y0;

        var srcStride = srcWidth * 4;

        for (var ch = 0; ch < 4; ch++)
        {
            var v00 = sourceRgba[y0 * srcStride + x0 * 4 + ch];
            var v10 = sourceRgba[y0 * srcStride + x1 * 4 + ch];
            var v01 = sourceRgba[y1 * srcStride + x0 * 4 + ch];
            var v11 = sourceRgba[y1 * srcStride + x1 * 4 + ch];

            var top = v00 + (v10 - v00) * fx;
            var bottom = v01 + (v11 - v01) * fx;
            var value = top + (bottom - top) * fy;

            dest[destOffset + ch] = (byte)Math.Clamp(Math.Round(value), 0, 255);
        }
    }
}
