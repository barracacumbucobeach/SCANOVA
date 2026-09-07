using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SCANOVA.Pdf.PdfRasterizer;
using SCANOVA.Pdf.PdfWriter;
using SCANOVA.Tiff.TiffEncoder;
using Xunit;

namespace SCANOVA.Pdf.Tests;

#pragma warning disable CA1416 // Projeto de teste multiplataforma chamando APIs marcadas como suportadas em Windows/Linux/macOS — ver comentário equivalente em PdfSharpPdfServiceTests.
public class PdfToImagePdfRasterizerTests
{
    private readonly IPdfRasterizer _sut = new PdfToImagePdfRasterizer();
    private readonly IPdfService _pdfService = new PdfSharpPdfService(new LibTiffEncoder());

    private static string TempPdfPath() => Path.Combine(Path.GetTempPath(), $"scanova-rasterizer-{Guid.NewGuid():N}.pdf");

    private async Task<string> CreateTestPdfAsync(int width, int height, double dpi, int pageCount = 1)
    {
        var pages = new RasterImage[pageCount];
        for (var i = 0; i < pageCount; i++)
        {
            pages[i] = TestImages.CreateGray(width, height, dpi);
        }

        var path = TempPdfPath();
        var settings = new PdfSettings { Mode = SCANOVA.Core.Enums.PdfMode.Grayscale, Dpi = dpi };
        await _pdfService.WritePdfAsync(pages, settings, path);
        return path;
    }

    [Fact]
    public async Task GetPageCountAsync_MultiPagePdf_ReturnsCorrectCount()
    {
        var path = await CreateTestPdfAsync(50, 50, 200, pageCount: 4);
        try
        {
            Assert.Equal(4, await _sut.GetPageCountAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RasterizePageAsync_DifferentDpiThanSource_ScalesPixelDimensionsAccordingly()
    {
        // 400x300 px a 200 DPI = 2,0 x 1,5 polegadas de página.
        var path = await CreateTestPdfAsync(400, 300, 200);

        try
        {
            var result = await _sut.RasterizePageAsync(path, 0, dpi: 100);

            // Rasterizado a 100 DPI, 2,0x1,5 polegadas vira 200x150 px.
            Assert.InRange(result.Width, 198, 202);
            Assert.InRange(result.Height, 148, 152);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RasterizeAllAsync_MultiPagePdf_YieldsOnePagePerPdfPage()
    {
        var path = await CreateTestPdfAsync(60, 40, 200, pageCount: 3);

        try
        {
            var count = 0;
            await foreach (var page in _sut.RasterizeAllAsync(path, dpi: 150))
            {
                count++;
                Assert.True(page.Width > 0);
                Assert.True(page.Height > 0);
            }

            Assert.Equal(3, count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task GetPageCountAsync_MissingFile_Throws()
    {
        await Assert.ThrowsAsync<PdfProcessingException>(() =>
            _sut.GetPageCountAsync(Path.Combine(Path.GetTempPath(), $"nao-existe-{Guid.NewGuid():N}.pdf")));
    }

    [Fact]
    public async Task RasterizePageAsync_InvalidDpi_Throws()
    {
        var path = await CreateTestPdfAsync(20, 20, 200);
        try
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _sut.RasterizePageAsync(path, 0, dpi: 0));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RasterizePageAsync_PageIndexOutOfRange_ThrowsPdfProcessingException()
    {
        var path = await CreateTestPdfAsync(20, 20, 200);
        try
        {
            await Assert.ThrowsAsync<PdfProcessingException>(() => _sut.RasterizePageAsync(path, 5, dpi: 100));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
#pragma warning restore CA1416
