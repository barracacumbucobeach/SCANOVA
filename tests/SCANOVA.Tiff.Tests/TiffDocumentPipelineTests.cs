using SCANOVA.Core.Enums;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SCANOVA.Imaging.ImageProcessing;
using SCANOVA.Tiff.TiffEncoder;
using SCANOVA.Tiff.TiffValidator;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;
using Xunit;

namespace SCANOVA.Tiff.Tests;

/// <summary>
/// Testes de ponta a ponta do marco crítico do produto (seção 125 — critério de aceite do
/// TIFF; seção 150 cenários A/B): imagem qualquer → "Salvar TIFF Documental" → validado.
/// </summary>
public class TiffDocumentPipelineTests
{
    private readonly IImageService _imageService = new SkiaImageService();
    private readonly ITiffEncoder _encoder = new LibTiffEncoder();
    private readonly ITiffValidator _validator = new LibTiffValidator();

    private ITiffDocumentPipeline CreateSut() => new SCANOVA.Tiff.TiffDocumentPipeline(_imageService, _encoder, _validator);

    private static string TempTiffPath() => Path.Combine(Path.GetTempPath(), $"scanova-pipeline-{Guid.NewGuid():N}.tif");

    [Fact]
    public async Task SaveDocumentalTiffAsync_FromColorImage_ProducesFullyValidDocumentalTiff()
    {
        // Cenário B da seção 150: "Abrir JPG → Melhorar → Converter → TIFF Documental → 200 DPI → CCITT Group 4".
        var source = TestImages.CreateDocumentLike(500, 350, dpi: 96); // DPI de origem diferente de 200, de propósito
        var sut = CreateSut();
        var path = TempTiffPath();

        try
        {
            var result = await sut.SaveDocumentalTiffAsync(source, path);

            Assert.True(result.Success, result.UserMessage);
            Assert.Equal(path, result.OutputPath);
            Assert.True(File.Exists(path));

            // Critério de aceite completo (seção 125): abre corretamente, 200 DPI, 1 bit,
            // CCITT Group 4, não corrompido, passa pelo validador interno.
            var report = await _validator.ValidateDocumentalAsync(path);
            Assert.True(report.IsValid, report.UserMessage);
            Assert.True(report.IsReadableTiff);
            Assert.True(report.IsDecodable);
            Assert.True(report.Is1Bit);
            Assert.True(report.IsCcittGroup4);
            Assert.True(report.MatchesDpi(200));

            // Seção 24: a dimensão em pixels deve refletir a normalização de DPI (96 → 200).
            var expectedWidth = (int)Math.Round(500 * 200.0 / 96.0);
            Assert.Equal(expectedWidth, report.Width);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SaveDocumentalTiffAsync_NeverModifiesSourceImage()
    {
        var source = TestImages.CreateDocumentLike(200, 150);
        var originalPixelsCopy = (byte[])source.Pixels.Clone();
        var originalFormat = source.Format;
        var sut = CreateSut();
        var path = TempTiffPath();

        try
        {
            await sut.SaveDocumentalTiffAsync(source, path);

            Assert.Equal(originalFormat, source.Format);
            Assert.Equal(originalPixelsCopy, source.Pixels); // seção 45: edição não destrutiva
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SaveDocumentalTiffAsync_CompressesSignificantlySmallerThanRawPixels()
    {
        // Seção 90: a compressão deve ser eficiente de verdade, não apenas alegada.
        var source = TestImages.CreateDocumentLike(600, 800);
        var sut = CreateSut();
        var path = TempTiffPath();

        try
        {
            var result = await sut.SaveDocumentalTiffAsync(source, path);

            Assert.True(result.Success);
            var rawPixelBytes = (long)source.Width * source.Height * 4; // RGBA32 original
            Assert.True(result.OutputSizeBytes < rawPixelBytes / 4, $"TIFF G4 ({result.OutputSizeBytes} bytes) deveria ser bem menor que os pixels brutos ({rawPixelBytes} bytes).");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SaveDocumentalTiffAsync_WithGlobalThreshold_UsesProvidedValue()
    {
        var source = TestImages.CreateDocumentLike(120, 90);
        var adjustments = new ImageAdjustments { ThresholdMethod = ThresholdMethod.Global, GlobalThreshold = 200 };
        var sut = CreateSut();
        var path = TempTiffPath();

        try
        {
            var result = await sut.SaveDocumentalTiffAsync(source, path, adjustments);

            Assert.True(result.Success, result.UserMessage);
            var report = await _validator.ValidateDocumentalAsync(path);
            Assert.True(report.IsValid, report.UserMessage);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SaveDocumentalTiffAsync_WithAdaptiveThreshold_StillProducesValidDocumentalTiff()
    {
        var source = TestImages.CreateDocumentLike(300, 200);
        var adjustments = new ImageAdjustments { ThresholdMethod = ThresholdMethod.Adaptive };
        var sut = CreateSut();
        var path = TempTiffPath();

        try
        {
            var result = await sut.SaveDocumentalTiffAsync(source, path, adjustments);

            Assert.True(result.Success, result.UserMessage);
            var report = await _validator.ValidateDocumentalAsync(path);
            Assert.True(report.IsValid, report.UserMessage);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SaveDocumentalTiffMultiPageAsync_ProducesValidMultiPageDocumentalTiff()
    {
        // Seção 68/70: documento composto por várias páginas em um único TIFF.
        var pages = new[]
        {
            TestImages.CreateDocumentLike(200, 150),
            TestImages.CreateDocumentLike(200, 150),
            TestImages.CreateDocumentLike(200, 150),
        };
        var sut = CreateSut();
        var path = TempTiffPath();

        try
        {
            var result = await sut.SaveDocumentalTiffMultiPageAsync(pages, path);

            Assert.True(result.Success, result.UserMessage);
            var report = await _validator.ValidateDocumentalAsync(path);
            Assert.True(report.IsValid, report.UserMessage);
            Assert.Equal(3, report.PageCount);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SaveDocumentalTiffAsync_FromAlreadyGrayscaleSource_Works()
    {
        var source = TestImages.CreateGray(150, 100, (x, y) => (byte)((x + y) % 256));
        var sut = CreateSut();
        var path = TempTiffPath();

        try
        {
            var result = await sut.SaveDocumentalTiffAsync(source, path);

            Assert.True(result.Success, result.UserMessage);
            var report = await _validator.ValidateDocumentalAsync(path);
            Assert.True(report.IsValid, report.UserMessage);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SaveDocumentalTiffAsync_InvalidDestinationFolder_ReturnsFriendlyFailure()
    {
        var source = TestImages.CreateDocumentLike(50, 50);
        var sut = CreateSut();

        // Caminho com um segmento de diretório inválido (NUL em Linux/Windows não é permitido em nomes).
        var invalidPath = Path.Combine(Path.GetTempPath(), "scanova-invalid-\0-dir", "out.tif");

        var result = await sut.SaveDocumentalTiffAsync(source, invalidPath);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.UserMessage));
        Assert.DoesNotContain("Exception", result.UserMessage, StringComparison.OrdinalIgnoreCase);
    }
}
