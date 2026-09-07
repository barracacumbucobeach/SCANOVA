using SCANOVA.Core.Enums;
using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SCANOVA.Tiff.TiffEncoder;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;
using Xunit;

namespace SCANOVA.Tiff.Tests;

public class TiffEncoderTests
{
    private readonly ITiffEncoder _sut = new LibTiffEncoder();

    private static string TempTiffPath() => Path.Combine(Path.GetTempPath(), $"scanova-tiff-{Guid.NewGuid():N}.tif");

    [Fact]
    public async Task EncodeAsync_Bilevel_RoundTripsExactBits()
    {
        var image = TestImages.CreateBilevel(64, 48);
        var path = TempTiffPath();

        try
        {
            await _sut.EncodeAsync(image, TiffSettings.Documental, path);
            Assert.True(File.Exists(path));

            var decodedPages = await _sut.DecodeAsync(path);

            Assert.Single(decodedPages);
            var decoded = decodedPages[0];
            Assert.Equal(CorePixelFormat.Bilevel1, decoded.Format);
            Assert.Equal(image.Width, decoded.Width);
            Assert.Equal(image.Height, decoded.Height);
            Assert.Equal(image.Pixels, decoded.Pixels); // CCITT G4 é sem perdas — os bits devem ser idênticos.
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task EncodeAsync_WritesDpiAndResolutionUnit()
    {
        var image = TestImages.CreateBilevel(32, 32, dpi: 200);
        var path = TempTiffPath();

        try
        {
            await _sut.EncodeAsync(image, TiffSettings.Documental, path);

            var validator = new SCANOVA.Tiff.TiffValidator.LibTiffValidator();
            var report = await validator.ValidateAsync(path);

            Assert.Equal(200, report.HorizontalDpi);
            Assert.Equal(200, report.VerticalDpi);
            Assert.True(report.ResolutionUnitIsInch);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task EncodeAsync_ColorModeMismatch_ThrowsBeforeTouchingDisk()
    {
        var rgbaImage = TestImages.CreateDocumentLike(20, 20); // Rgba32, não binarizada

        var path = TempTiffPath();
        try
        {
            await Assert.ThrowsAsync<TiffEncodingException>(() =>
                _sut.EncodeAsync(rgbaImage, TiffSettings.Documental, path));

            Assert.False(File.Exists(path));
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
    public async Task EncodeAsync_CcittGroup4WithNonBilevelImage_Throws()
    {
        var gray = TestImages.CreateGray(20, 20, (_, _) => 128);
        var settings = new TiffSettings
        {
            HorizontalDpi = 200,
            VerticalDpi = 200,
            ColorMode = ColorMode.Grayscale,
            Compression = CompressionType.CcittGroup4, // combinação inválida: G4 exige bilevel
        };

        await Assert.ThrowsAsync<TiffEncodingException>(() =>
            _sut.EncodeAsync(gray, settings, TempTiffPath()));
    }

    [Fact]
    public async Task EncodeMultiPageAsync_RoundTripsAllPagesInOrder()
    {
        var pages = new[]
        {
            TestImages.CreateBilevel(40, 30),
            TestImages.CreateBilevel(50, 20),
            TestImages.CreateBilevel(30, 30),
        };
        var path = TempTiffPath();

        try
        {
            await _sut.EncodeMultiPageAsync(pages, TiffSettings.Documental, path);

            var decoded = await _sut.DecodeAsync(path);

            Assert.Equal(3, decoded.Count);
            for (var i = 0; i < pages.Length; i++)
            {
                Assert.Equal(pages[i].Width, decoded[i].Width);
                Assert.Equal(pages[i].Height, decoded[i].Height);
                Assert.Equal(pages[i].Pixels, decoded[i].Pixels);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task EncodeAsync_Grayscale_RoundTrips()
    {
        var image = TestImages.CreateGray(30, 20, (x, y) => (byte)((x * 7 + y * 13) % 256));
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
            await _sut.EncodeAsync(image, settings, path);
            var decoded = (await _sut.DecodeAsync(path))[0];

            Assert.Equal(CorePixelFormat.Gray8, decoded.Format);
            Assert.Equal(image.Pixels, decoded.Pixels);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task EncodeAsync_Rgb24_RoundTrips()
    {
        var image = TestImages.CreateRgb24(25, 18);
        var settings = new TiffSettings
        {
            HorizontalDpi = 300,
            VerticalDpi = 300,
            ColorMode = ColorMode.Color,
            Compression = CompressionType.Lzw,
        };
        var path = TempTiffPath();

        try
        {
            await _sut.EncodeAsync(image, settings, path);
            var decoded = (await _sut.DecodeAsync(path))[0];

            Assert.Equal(CorePixelFormat.Rgb24, decoded.Format);
            Assert.Equal(image.Pixels, decoded.Pixels);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task DecodeAsync_FileDoesNotExist_ThrowsImageLoadException()
    {
        await Assert.ThrowsAsync<ImageLoadException>(() => _sut.DecodeAsync(TempTiffPath()));
    }

    [Fact]
    public async Task DecodeAsync_CorruptedFile_ThrowsImageLoadException()
    {
        var path = TempTiffPath();
        await File.WriteAllBytesAsync(path, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        try
        {
            await Assert.ThrowsAsync<ImageLoadException>(() => _sut.DecodeAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task EncodeAsync_NeverModifiesSourceImage()
    {
        var image = TestImages.CreateBilevel(20, 20);
        var originalPixelsCopy = (byte[])image.Pixels.Clone();
        var path = TempTiffPath();

        try
        {
            await _sut.EncodeAsync(image, TiffSettings.Documental, path);
            Assert.Equal(originalPixelsCopy, image.Pixels);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
