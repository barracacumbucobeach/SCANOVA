using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SCANOVA.Imaging.ImageProcessing;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;
using Xunit;

namespace SCANOVA.Imaging.Tests;

public class PerspectiveTests
{
    private readonly IImageService _sut = new SkiaImageService();

    private static RasterImage CreateFourQuadrantImage(int width, int height)
    {
        // TL=vermelho, TR=verde, BR=azul, BL=amarelo.
        var stride = width * 4;
        var pixels = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = y * stride + x * 4;
                var isLeft = x < width / 2;
                var isTop = y < height / 2;
                (byte r, byte g, byte b) color = (isTop, isLeft) switch
                {
                    (true, true) => ((byte)255, (byte)0, (byte)0),   // TL vermelho
                    (true, false) => ((byte)0, (byte)255, (byte)0),  // TR verde
                    (false, false) => ((byte)0, (byte)0, (byte)255), // BR azul
                    (false, true) => ((byte)255, (byte)255, (byte)0), // BL amarelo
                };
                pixels[offset] = color.r;
                pixels[offset + 1] = color.g;
                pixels[offset + 2] = color.b;
                pixels[offset + 3] = 255;
            }
        }

        return new RasterImage(width, height, stride, CorePixelFormat.Rgba32, pixels, 200, 200);
    }

    private static (byte r, byte g, byte b) PixelAt(RasterImage image, int x, int y)
    {
        var offset = y * image.Stride + x * 4;
        return (image.Pixels[offset], image.Pixels[offset + 1], image.Pixels[offset + 2]);
    }

    [Fact]
    public void CorrectPerspective_QuadMatchingImageBounds_PreservesCornerLayout()
    {
        var image = CreateFourQuadrantImage(100, 100);
        var quad = CropRegion.FromRectangle(0, 0, 100, 100);

        var result = _sut.CorrectPerspective(image, quad, 100, 100);

        // Amostra perto de cada canto (não exatamente no pixel de borda) — a propriedade
        // matemática da transformação garante que (u,v)→(0,0) mapeia para TopLeft do quad,
        // (1,0)→TopRight, (1,1)→BottomRight, (0,1)→BottomLeft, para qualquer quadrilátero.
        Assert.Equal((255, 0, 0), PixelAt(result, 2, 2));       // TL vermelho
        Assert.Equal((0, 255, 0), PixelAt(result, 97, 2));      // TR verde
        Assert.Equal((0, 0, 255), PixelAt(result, 97, 97));     // BR azul
        Assert.Equal((255, 255, 0), PixelAt(result, 2, 97));    // BL amarelo
    }

    [Fact]
    public void CorrectPerspective_RotatedSquareRegion_ExtractsUniformInteriorColor()
    {
        const int canvasSize = 200;
        const double halfSize = 40;
        const double angleDegrees = 20;
        var center = (x: 100.0, y: 100.0);

        var corners = RotatedSquareCorners(center, halfSize, angleDegrees);

        // Pinta o fundo de preto e a região do quadrado rotacionado de branco.
        var stride = canvasSize * 4;
        var pixels = new byte[stride * canvasSize];
        var angleRad = -angleDegrees * Math.PI / 180.0; // inversa, para o teste de "está dentro"
        for (var y = 0; y < canvasSize; y++)
        {
            for (var x = 0; x < canvasSize; x++)
            {
                var dx = x - center.x;
                var dy = y - center.y;
                var rx = dx * Math.Cos(angleRad) - dy * Math.Sin(angleRad);
                var ry = dx * Math.Sin(angleRad) + dy * Math.Cos(angleRad);
                var inside = Math.Abs(rx) <= halfSize && Math.Abs(ry) <= halfSize;

                var offset = y * stride + x * 4;
                byte v = inside ? (byte)255 : (byte)0;
                pixels[offset] = v;
                pixels[offset + 1] = v;
                pixels[offset + 2] = v;
                pixels[offset + 3] = 255;
            }
        }

        var image = new RasterImage(canvasSize, canvasSize, stride, CorePixelFormat.Rgba32, pixels, 200, 200);
        var quad = new CropRegion
        {
            TopLeft = new DocumentPoint(corners.tl.x, corners.tl.y),
            TopRight = new DocumentPoint(corners.tr.x, corners.tr.y),
            BottomRight = new DocumentPoint(corners.br.x, corners.br.y),
            BottomLeft = new DocumentPoint(corners.bl.x, corners.bl.y),
        };

        var result = _sut.CorrectPerspective(image, quad, 80, 80);

        // O interior (longe das bordas, onde pode haver alguma mistura com o fundo) deve ser
        // uniformemente branco — prova de que a região rotacionada foi corretamente endireitada.
        for (var y = 15; y < 65; y += 10)
        {
            for (var x = 15; x < 65; x += 10)
            {
                var (r, g, b) = PixelAt(result, x, y);
                Assert.True(r > 240 && g > 240 && b > 240, $"Pixel ({x},{y}) deveria ser branco, mas foi ({r},{g},{b}).");
            }
        }
    }

    private static ((double x, double y) tl, (double x, double y) tr, (double x, double y) br, (double x, double y) bl)
        RotatedSquareCorners((double x, double y) center, double halfSize, double angleDegrees)
    {
        var angleRad = angleDegrees * Math.PI / 180.0;

        (double x, double y) Rotate(double relX, double relY)
        {
            var rx = relX * Math.Cos(angleRad) - relY * Math.Sin(angleRad);
            var ry = relX * Math.Sin(angleRad) + relY * Math.Cos(angleRad);
            return (center.x + rx, center.y + ry);
        }

        return (
            tl: Rotate(-halfSize, -halfSize),
            tr: Rotate(halfSize, -halfSize),
            br: Rotate(halfSize, halfSize),
            bl: Rotate(-halfSize, halfSize));
    }

    [Fact]
    public void CorrectPerspective_InvalidOutputDimensions_Throws()
    {
        var image = CreateFourQuadrantImage(20, 20);
        var quad = CropRegion.FromRectangle(0, 0, 20, 20);

        Assert.Throws<ArgumentOutOfRangeException>(() => _sut.CorrectPerspective(image, quad, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => _sut.CorrectPerspective(image, quad, 10, 0));
    }
}
