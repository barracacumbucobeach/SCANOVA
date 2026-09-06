using SCANOVA.Core.Models;
using SCANOVA.Imaging.Detection;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;
using Xunit;

namespace SCANOVA.Imaging.Tests;

public class ProjectionProfileSkewEstimatorTests
{
    /// <summary>
    /// Gera uma imagem com "linhas de texto" (faixas horizontais escuras finas, espaçadas
    /// regularmente) rotacionadas por <paramref name="angleDegrees"/>, usando a mesma convenção
    /// de rotação de ponto (x = x0·cosθ − y0·sinθ, y = x0·sinθ + y0·cosθ) que
    /// <c>IImageService.Rotate</c> (via SkiaSharp) usa para pixels de origem — ver comentário em
    /// <see cref="ProjectionProfileSkewEstimator"/>.
    /// </summary>
    private static RasterImage CreateRotatedTextLines(int size, double angleDegrees, int lineSpacing = 10, int lineThickness = 3)
    {
        var pixels = new byte[size * size];
        Array.Fill(pixels, (byte)255);

        var angleRad = angleDegrees * Math.PI / 180.0;
        var cos = Math.Cos(angleRad);
        var sin = Math.Sin(angleRad);
        var center = size / 2.0;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                // Desfaz a rotação para encontrar a coordenada "ideal" (não rotacionada) deste
                // pixel de destino, e decide se ela cai dentro de uma linha de texto horizontal.
                var dx = x - center;
                var dy = y - center;
                var idealY = -dx * sin + dy * cos;

                var mod = ((idealY % lineSpacing) + lineSpacing) % lineSpacing;
                if (mod < lineThickness)
                {
                    pixels[y * size + x] = 30;
                }
            }
        }

        return new RasterImage(size, size, size, CorePixelFormat.Gray8, pixels, 200, 200);
    }

    [Fact]
    public void EstimateSkewAngleDegrees_UnrotatedLines_ReturnsNearZero()
    {
        var image = CreateRotatedTextLines(200, angleDegrees: 0);

        var angle = ProjectionProfileSkewEstimator.EstimateSkewAngleDegrees(image, threshold: 128);

        Assert.InRange(angle, -0.5, 0.5);
    }

    [Theory]
    [InlineData(7.0)]
    [InlineData(-4.0)]
    public void EstimateSkewAngleDegrees_RotatedLines_RecoversAngle(double trueAngle)
    {
        var image = CreateRotatedTextLines(200, trueAngle);

        var angle = ProjectionProfileSkewEstimator.EstimateSkewAngleDegrees(image, threshold: 128);

        Assert.InRange(angle, trueAngle - 0.5, trueAngle + 0.5);
    }

    [Fact]
    public void EstimateSkewAngleDegrees_BlankImage_ReturnsZeroWithoutThrowing()
    {
        var pixels = new byte[100 * 100];
        Array.Fill(pixels, (byte)255);
        var image = new RasterImage(100, 100, 100, CorePixelFormat.Gray8, pixels, 200, 200);

        var angle = ProjectionProfileSkewEstimator.EstimateSkewAngleDegrees(image, threshold: 128);

        Assert.Equal(0.0, angle);
    }
}
