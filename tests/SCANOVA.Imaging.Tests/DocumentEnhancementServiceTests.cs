using SCANOVA.Core.Enums;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SCANOVA.Imaging.Detection;
using SCANOVA.Imaging.Enhancement;
using SCANOVA.Imaging.ImageProcessing;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;
using Xunit;

namespace SCANOVA.Imaging.Tests;

public class DocumentEnhancementServiceTests
{
    private readonly IImageService _imageService = new SkiaImageService();
    private readonly IDocumentDetectionService _detectionService;
    private readonly IDocumentEnhancementService _sut;

    public DocumentEnhancementServiceTests()
    {
        _detectionService = new DocumentDetectionService(_imageService);
        _sut = new DocumentEnhancementService(_imageService, _detectionService);
    }

    private static RasterImage CreateHorizontalLinesImage(int size, int lineSpacing = 10, int lineThickness = 3)
    {
        var pixels = new byte[size * size];
        Array.Fill(pixels, (byte)255);

        for (var y = 0; y < size; y++)
        {
            if (y % lineSpacing < lineThickness)
            {
                for (var x = 0; x < size; x++)
                {
                    pixels[y * size + x] = 30;
                }
            }
        }

        return new RasterImage(size, size, size, CorePixelFormat.Gray8, pixels, 200, 200);
    }

    [Fact]
    public void Apply_NeutralAdjustments_ReturnsEquivalentImage()
    {
        var image = CreateHorizontalLinesImage(100);

        var result = _sut.Apply(image, ImageAdjustments.None);

        Assert.Equal(image.Width, result.Width);
        Assert.Equal(image.Height, result.Height);
        Assert.Equal(image.Format, result.Format);
        Assert.Equal(image.Pixels, result.Pixels);
    }

    [Fact]
    public void Apply_CorrectSkew_StraightensRotatedContent()
    {
        // Gira uma imagem de "linhas de texto" por um ângulo conhecido usando o próprio
        // IImageService.Rotate (a mesma rotação real usada em produção) — este teste é a
        // verificação de ponta a ponta de que o sinal usado por CorrectSkew (estimador +
        // Rotate(-ângulo)) realmente endireita o conteúdo, e não o inclina ainda mais.
        var original = CreateHorizontalLinesImage(160);
        var rotated = _imageService.Rotate(original, 6.0);
        var rotatedGray = rotated.Format == CorePixelFormat.Gray8 ? rotated : _imageService.ToGrayscale(rotated);

        var adjustments = new ImageAdjustments { CorrectSkew = true };
        var corrected = _sut.Apply(rotatedGray, adjustments);
        var correctedGray = corrected.Format == CorePixelFormat.Gray8 ? corrected : _imageService.ToGrayscale(corrected);

        var threshold = _imageService.ComputeOtsuThreshold(correctedGray);
        var residualAngle = ProjectionProfileSkewEstimator.EstimateSkewAngleDegrees(correctedGray, threshold);

        Assert.InRange(Math.Abs(residualAngle), 0, 1.0);
    }

    [Fact]
    public void Apply_Binarize_ProducesBilevelOutput()
    {
        var image = CreateHorizontalLinesImage(80);

        var result = _sut.Apply(image, new ImageAdjustments { Binarize = true });

        Assert.Equal(CorePixelFormat.Bilevel1, result.Format);
    }

    [Fact]
    public void Apply_RemoveBackgroundWithoutExplicitGrayscaleFlag_StillConvertsAndSucceeds()
    {
        // RemoveBackground exige Gray8 na camada de baixo nível — o orquestrador deve converter
        // automaticamente mesmo quando ConvertToGrayscale não foi marcado explicitamente.
        var pixels = new byte[40 * 40 * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 200; pixels[i + 1] = 200; pixels[i + 2] = 200; pixels[i + 3] = 255;
        }
        var image = new RasterImage(40, 40, 40 * 4, CorePixelFormat.Rgba32, pixels, 200, 200);

        var result = _sut.Apply(image, new ImageAdjustments { RemoveBackground = true });

        Assert.Equal(CorePixelFormat.Gray8, result.Format);
    }

    [Theory]
    [InlineData(EnhancementPreset.Light)]
    [InlineData(EnhancementPreset.Normal)]
    [InlineData(EnhancementPreset.Strong)]
    public void ResolvePreset_KnownPresets_EnableSkewAndPerspectiveCorrection(EnhancementPreset preset)
    {
        var adjustments = _sut.ResolvePreset(preset);

        Assert.True(adjustments.CorrectSkew);
        Assert.True(adjustments.CorrectPerspective);
    }

    [Fact]
    public void ResolvePreset_Custom_ReturnsNeutralBaseline()
    {
        var adjustments = _sut.ResolvePreset(EnhancementPreset.Custom);

        Assert.Equal(ImageAdjustments.None, adjustments);
    }

    [Fact]
    public void AutoEnhance_NormalPreset_ProducesGrayscaleOutput()
    {
        var image = CreateHorizontalLinesImage(120);

        var result = _sut.AutoEnhance(image, EnhancementPreset.Normal);

        Assert.Equal(CorePixelFormat.Gray8, result.Format);
    }
}
