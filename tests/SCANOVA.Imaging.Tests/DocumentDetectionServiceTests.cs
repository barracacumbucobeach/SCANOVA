using SCANOVA.Core.Enums;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SCANOVA.Imaging.Detection;
using SCANOVA.Imaging.ImageProcessing;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;
using Xunit;

namespace SCANOVA.Imaging.Tests;

public class DocumentDetectionServiceTests
{
    private readonly IDocumentDetectionService _sut = new DocumentDetectionService(new SkiaImageService());

    private static RasterImage CreateDocumentOnBackground(int canvasSize, int docLeft, int docTop, int docWidth, int docHeight, byte docValue = 235, byte backgroundValue = 20)
    {
        var pixels = new byte[canvasSize * canvasSize];
        Array.Fill(pixels, backgroundValue);

        for (var y = docTop; y < docTop + docHeight; y++)
        {
            for (var x = docLeft; x < docLeft + docWidth; x++)
            {
                pixels[y * canvasSize + x] = docValue;
            }
        }

        return new RasterImage(canvasSize, canvasSize, canvasSize, CorePixelFormat.Gray8, pixels, 200, 200);
    }

    [Fact]
    public async Task DetectAsync_LightDocumentOnDarkBackground_FindsAxisAlignedRegionWithHighConfidence()
    {
        var image = CreateDocumentOnBackground(200, docLeft: 40, docTop: 30, docWidth: 100, docHeight: 120);

        var result = await _sut.DetectAsync(image);

        Assert.True(result.DocumentFound);
        Assert.NotNull(result.Region);
        Assert.True(result.Confidence > 0.9, $"Confiança esperada alta para um retângulo bem definido, obtida {result.Confidence}.");

        // O quadrilátero detectado deve corresponder de perto ao retângulo real do documento.
        var region = result.Region!;
        Assert.InRange(region.TopLeft.X, 38, 42);
        Assert.InRange(region.TopLeft.Y, 28, 32);
        Assert.InRange(region.BottomRight.X, 138, 142);
        Assert.InRange(region.BottomRight.Y, 148, 152);
    }

    [Fact]
    public async Task DetectAsync_DarkDocumentOnLightBackground_StillFindsRegion()
    {
        // Polaridade invertida: documento escuro sobre fundo claro — a detecção não deve assumir
        // qual classe é o documento.
        var image = CreateDocumentOnBackground(200, docLeft: 50, docTop: 50, docWidth: 80, docHeight: 90, docValue: 20, backgroundValue: 235);

        var result = await _sut.DetectAsync(image);

        Assert.True(result.DocumentFound);
        Assert.True(result.Confidence > 0.9);
    }

    [Fact]
    public async Task DetectAsync_UniformImage_ReturnsNotFound()
    {
        var pixels = new byte[100 * 100];
        Array.Fill(pixels, (byte)200);
        var image = new RasterImage(100, 100, 100, CorePixelFormat.Gray8, pixels, 200, 200);

        var result = await _sut.DetectAsync(image);

        Assert.False(result.DocumentFound);
        Assert.Equal(0.0, result.Confidence);
    }

    [Fact]
    public async Task DetectAsync_RotatedDocument_ReportsNonZeroSkewAngle()
    {
        const int canvasSize = 200;
        const double halfSize = 50;
        const double angleDegrees = 12;
        var center = canvasSize / 2.0;
        var angleRad = angleDegrees * Math.PI / 180.0;
        var cos = Math.Cos(angleRad);
        var sin = Math.Sin(angleRad);

        var pixels = new byte[canvasSize * canvasSize];
        for (var y = 0; y < canvasSize; y++)
        {
            for (var x = 0; x < canvasSize; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var rx = dx * cos + dy * sin;
                var ry = -dx * sin + dy * cos;
                var inside = Math.Abs(rx) <= halfSize && Math.Abs(ry) <= halfSize;
                pixels[y * canvasSize + x] = inside ? (byte)230 : (byte)20;
            }
        }

        var image = new RasterImage(canvasSize, canvasSize, canvasSize, CorePixelFormat.Gray8, pixels, 200, 200);

        var result = await _sut.DetectAsync(image);

        Assert.True(result.DocumentFound);
        Assert.InRange(Math.Abs(result.SkewAngleDegrees), 8, 16);
    }
}
