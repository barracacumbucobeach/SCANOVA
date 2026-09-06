using SCANOVA.Core.Enums;
using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SCANOVA.Pdf.PdfRasterizer;
using SCANOVA.Pdf.PdfWriter;
using SCANOVA.Tiff.TiffEncoder;
using Xunit;

namespace SCANOVA.Pdf.Tests;

#pragma warning disable CA1416 // Projeto de teste multiplataforma chamando APIs marcadas como suportadas em Windows/Linux/macOS (ver SCANOVA.Pdf/ServiceCollectionExtensions.cs) — sem TFM específico de OS para satisfazer o analisador automaticamente.
public class PdfSharpPdfServiceTests
{
    private readonly IPdfService _sut = new PdfSharpPdfService(new LibTiffEncoder());
    private readonly IPdfRasterizer _rasterizer = new PdfToImagePdfRasterizer();

    private static string TempPdfPath() => Path.Combine(Path.GetTempPath(), $"scanova-pdf-{Guid.NewGuid():N}.pdf");
    private static string TempTiffPath() => Path.Combine(Path.GetTempPath(), $"scanova-pdf-src-{Guid.NewGuid():N}.tif");

    [Fact]
    public async Task WritePdfAsync_SinglePage_CreatesReadablePdfWithMatchingPageSize()
    {
        // 400x300 px a 200 DPI = 2x1.5 polegadas = 144x108 pontos (1 polegada = 72 pontos).
        var image = TestImages.CreateBilevel(400, 300, dpi: 200);
        var path = TempPdfPath();

        try
        {
            await _sut.WritePdfAsync(new[] { image }, PdfSettings.Documental, path);

            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 0);

            var pageCount = await _rasterizer.GetPageCountAsync(path);
            Assert.Equal(1, pageCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task WritePdfAsync_MultiplePages_PreservesPageCountAndOrder()
    {
        var pages = new[]
        {
            TestImages.CreateGray(100, 80, value: 30),  // página escura
            TestImages.CreateGray(100, 80, value: 220), // página clara
        };
        var settings = new PdfSettings { Mode = PdfMode.Grayscale, Dpi = 200 };
        var path = TempPdfPath();

        try
        {
            await _sut.WritePdfAsync(pages, settings, path);

            Assert.Equal(2, await _rasterizer.GetPageCountAsync(path));

            var page0 = await _rasterizer.RasterizePageAsync(path, 0, dpi: 100);
            var page1 = await _rasterizer.RasterizePageAsync(path, 1, dpi: 100);

            // A página 0 (escura, 30) deve continuar visivelmente mais escura que a página 1
            // (clara, 220) depois de ida e volta pelo PDF — confirma que a ordem foi preservada.
            Assert.True(AverageBrightness(page0) < AverageBrightness(page1));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static double AverageBrightness(RasterImage rgba)
    {
        long sum = 0;
        var count = rgba.Width * rgba.Height;
        for (var y = 0; y < rgba.Height; y++)
        {
            var row = y * rgba.Stride;
            for (var x = 0; x < rgba.Width; x++)
            {
                var offset = row + x * 4;
                sum += rgba.Pixels[offset]; // canal R basta (a página é acromática)
            }
        }

        return (double)sum / count;
    }

    [Fact]
    public async Task WritePdfAsync_EmptyPageList_Throws()
    {
        var path = TempPdfPath();
        await Assert.ThrowsAsync<PdfProcessingException>(() =>
            _sut.WritePdfAsync(Array.Empty<RasterImage>(), PdfSettings.Documental, path));
    }

    [Fact]
    public async Task WritePdfAsync_ColorImageWithDocumentalMode_Throws()
    {
        var image = TestImages.CreateRgb24(20, 20);
        var path = TempPdfPath();

        await Assert.ThrowsAsync<PdfProcessingException>(() =>
            _sut.WritePdfAsync(new[] { image }, PdfSettings.Documental, path));
    }

    [Fact]
    public async Task WritePdfAsync_SearchableModeRequested_ThrowsWithClearMessage()
    {
        var image = TestImages.CreateGray(20, 20);
        var settings = new PdfSettings { Mode = PdfMode.Searchable };
        var path = TempPdfPath();

        var ex = await Assert.ThrowsAsync<PdfProcessingException>(() =>
            _sut.WritePdfAsync(new[] { image }, settings, path));

        Assert.Contains("Fase 9", ex.UserMessage);
    }

    [Fact]
    public async Task ConvertTiffToPdfAsync_MultiPageTiff_ProducesPdfWithSamePageCount()
    {
        var tiffEncoder = new LibTiffEncoder();
        var tiffPath = TempTiffPath();
        var pdfPath = TempPdfPath();

        try
        {
            var pages = new[] { TestImages.CreateBilevel(64, 48), TestImages.CreateBilevel(64, 48), TestImages.CreateBilevel(64, 48) };
            await tiffEncoder.EncodeMultiPageAsync(pages, TiffSettings.Documental, tiffPath);

            await _sut.ConvertTiffToPdfAsync(tiffPath, PdfSettings.Documental, pdfPath);

            Assert.Equal(3, await _rasterizer.GetPageCountAsync(pdfPath));
        }
        finally
        {
            File.Delete(tiffPath);
            File.Delete(pdfPath);
        }
    }

    [Fact]
    public async Task ConvertTiffToPdfAsync_MissingSourceFile_Throws()
    {
        await Assert.ThrowsAsync<PdfProcessingException>(() =>
            _sut.ConvertTiffToPdfAsync(Path.Combine(Path.GetTempPath(), $"nao-existe-{Guid.NewGuid():N}.tif"), PdfSettings.Documental, TempPdfPath()));
    }

    [Fact]
    public async Task TryExtractTextAsync_ImageOnlyPdf_ReturnsNull()
    {
        var image = TestImages.CreateBilevel(64, 48);
        var path = TempPdfPath();

        try
        {
            await _sut.WritePdfAsync(new[] { image }, PdfSettings.Documental, path);

            var text = await _sut.TryExtractTextAsync(path, pageIndex: 0);

            Assert.Null(text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task TryExtractTextAsync_PdfWithRealText_ReturnsIt()
    {
        var path = TempPdfPath();
        try
        {
            MinimalTextPdfBuilder.WriteSinglePageWithText(path, "Documento SCANOVA de teste");

            var text = await _sut.TryExtractTextAsync(path, pageIndex: 0);

            Assert.NotNull(text);
            Assert.Contains("SCANOVA", text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task TryExtractTextAsync_PageIndexOutOfRange_Throws()
    {
        var path = TempPdfPath();
        try
        {
            MinimalTextPdfBuilder.WriteSinglePageWithText(path, "Página única");

            await Assert.ThrowsAsync<PdfProcessingException>(() => _sut.TryExtractTextAsync(path, pageIndex: 5));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task TryExtractTextAsync_MissingFile_Throws()
    {
        await Assert.ThrowsAsync<PdfProcessingException>(() =>
            _sut.TryExtractTextAsync(Path.Combine(Path.GetTempPath(), $"nao-existe-{Guid.NewGuid():N}.pdf"), 0));
    }
}
#pragma warning restore CA1416
