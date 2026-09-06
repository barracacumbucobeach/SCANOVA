using SCANOVA.Core.Enums;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SCANOVA.Tiff.TiffEncoder;
using SCANOVA.Tiff.TiffValidator;
using Xunit;

namespace SCANOVA.Tiff.Tests;

/// <summary>
/// Testes do validador (seção 26/123/125). O critério de aceite do TIFF só é satisfeito quando
/// TODOS os itens do checklist passam: abrir corretamente, 200 DPI, 1 bit, CCITT Group 4, não
/// corrompido, validador interno aprova.
/// </summary>
public class TiffValidatorTests
{
    private readonly ITiffEncoder _encoder = new LibTiffEncoder();
    private readonly ITiffValidator _sut = new LibTiffValidator();

    private static string TempTiffPath() => Path.Combine(Path.GetTempPath(), $"scanova-validate-{Guid.NewGuid():N}.tif");

    [Fact]
    public async Task ValidateDocumentalAsync_ProperlyEncodedFile_PassesEveryChecklistItem()
    {
        var image = TestImages.CreateBilevel(64, 48);
        var path = TempTiffPath();

        try
        {
            await _encoder.EncodeAsync(image, TiffSettings.Documental, path);

            var report = await _sut.ValidateDocumentalAsync(path);

            // Seção 26: ✓ TIFF válido ✓ 200 DPI ✓ 1 bit ✓ CCITT Group 4 ✓ Arquivo legível
            Assert.True(report.IsValid);
            Assert.True(report.IsReadableTiff);
            Assert.True(report.IsDecodable);
            Assert.True(report.Is1Bit);
            Assert.True(report.IsCcittGroup4);
            Assert.True(report.MatchesDpi(200));
            Assert.Equal(64, report.Width);
            Assert.Equal(48, report.Height);
            Assert.Equal(1, report.PageCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ValidateDocumentalAsync_NonDocumentalFile_FailsWithClearMessage()
    {
        var gray = TestImages.CreateGray(20, 20, (_, _) => 100);
        var settings = new TiffSettings
        {
            HorizontalDpi = 300,
            VerticalDpi = 300,
            ColorMode = ColorMode.Grayscale,
            Compression = CompressionType.Lzw,
        };
        var path = TempTiffPath();

        try
        {
            await _encoder.EncodeAsync(gray, settings, path);

            var report = await _sut.ValidateDocumentalAsync(path);

            Assert.False(report.IsValid);
            Assert.False(string.IsNullOrWhiteSpace(report.UserMessage));
            // O arquivo em si continua legível/decodificável — só não satisfaz o preset documental.
            Assert.True(report.IsReadableTiff);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ValidateAsync_MissingFile_ReturnsInvalidWithFriendlyMessage()
    {
        var report = await _sut.ValidateAsync(TempTiffPath());

        Assert.False(report.IsValid);
        Assert.False(report.IsReadableTiff);
        Assert.False(string.IsNullOrWhiteSpace(report.UserMessage));
    }

    [Fact]
    public async Task ValidateAsync_NotATiffFile_ReturnsInvalid()
    {
        var path = TempTiffPath();
        await File.WriteAllTextAsync(path, "isto não é um TIFF");

        try
        {
            var report = await _sut.ValidateAsync(path);

            Assert.False(report.IsValid);
            Assert.False(report.IsReadableTiff);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ValidateAsync_MultiPageFile_ReportsCorrectPageCount()
    {
        var pages = new[] { TestImages.CreateBilevel(20, 20), TestImages.CreateBilevel(20, 20), TestImages.CreateBilevel(20, 20) };
        var path = TempTiffPath();

        try
        {
            await _encoder.EncodeMultiPageAsync(pages, TiffSettings.Documental, path);

            var report = await _sut.ValidateAsync(path);

            Assert.Equal(3, report.PageCount);
            Assert.True(report.IsValid);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
