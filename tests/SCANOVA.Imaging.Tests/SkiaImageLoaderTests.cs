using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Imaging.ImageLoading;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;
using Xunit;

namespace SCANOVA.Imaging.Tests;

public class SkiaImageLoaderTests
{
    private readonly IImageLoader _sut = new SkiaImageLoader();
    private readonly IImageExporter _exporter = new SkiaImageExporter();

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    public async Task LoadAsync_RoundTripsThroughExporter(string extension)
    {
        var original = TestImages.CreateDocumentLike();
        var path = Path.Combine(Path.GetTempPath(), $"scanova-test-{Guid.NewGuid():N}{extension}");
        try
        {
            await _exporter.SaveAsync(original, path);
            Assert.True(File.Exists(path));

            var loaded = await _sut.LoadAsync(path);

            Assert.Equal(original.Width, loaded.Width);
            Assert.Equal(original.Height, loaded.Height);
            Assert.Equal(CorePixelFormat.Rgba32, loaded.Format);
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
    public async Task LoadAsync_DecodesBmp()
    {
        // O Skia só decodifica BMP (não codifica — por isso SkiaImageExporter não oferece
        // ".bmp"). Aqui geramos um BMP 24-bit sem compressão manualmente, no formato de
        // arquivo documentado publicamente (BITMAPFILEHEADER + BITMAPINFOHEADER), só para
        // provar que o carregamento funciona.
        var path = Path.Combine(Path.GetTempPath(), $"scanova-bmp-{Guid.NewGuid():N}.bmp");
        WriteMinimalBmp(path, width: 8, height: 6);

        try
        {
            var loaded = await _sut.LoadAsync(path);

            Assert.Equal(8, loaded.Width);
            Assert.Equal(6, loaded.Height);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void WriteMinimalBmp(string path, int width, int height)
    {
        int rowSize = ((width * 3 + 3) / 4) * 4; // linhas alinhadas a 4 bytes
        int pixelDataSize = rowSize * height;
        int fileSize = 14 + 40 + pixelDataSize;

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        // BITMAPFILEHEADER
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(14 + 40); // offset dos dados de pixel

        // BITMAPINFOHEADER
        writer.Write(40);
        writer.Write(width);
        writer.Write(height); // positivo = bottom-up
        writer.Write((ushort)1); // planes
        writer.Write((ushort)24); // bits per pixel
        writer.Write(0); // sem compressão
        writer.Write(pixelDataSize);
        writer.Write(2835); // ~72 DPI
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);

        // Dados de pixel: BGR, bottom-up, linhas com padding a 4 bytes.
        var row = new byte[rowSize];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                row[x * 3] = 40;      // B
                row[x * 3 + 1] = 90;  // G
                row[x * 3 + 2] = 200; // R
            }
            writer.Write(row);
        }
    }

    [Fact]
    public async Task LoadAsync_FileDoesNotExist_ThrowsImageLoadExceptionWithUserMessage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"scanova-missing-{Guid.NewGuid():N}.png");

        var ex = await Assert.ThrowsAsync<ImageLoadException>(() => _sut.LoadAsync(path));

        Assert.False(string.IsNullOrWhiteSpace(ex.UserMessage));
        Assert.DoesNotContain("Exception", ex.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_CorruptedFile_ThrowsImageLoadException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"scanova-corrupt-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(path, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });

        try
        {
            await Assert.ThrowsAsync<ImageLoadException>(() => _sut.LoadAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CanLoadAsync_ReturnsFalse_ForMissingFile()
    {
        var result = await _sut.CanLoadAsync(Path.Combine(Path.GetTempPath(), $"scanova-nope-{Guid.NewGuid():N}.png"));
        Assert.False(result);
    }

    [Fact]
    public async Task CanLoadAsync_ReturnsTrue_ForValidImage()
    {
        var original = TestImages.CreateSolid(20, 20, 10, 20, 30);
        var path = Path.Combine(Path.GetTempPath(), $"scanova-valid-{Guid.NewGuid():N}.png");
        try
        {
            await _exporter.SaveAsync(original, path);
            Assert.True(await _sut.CanLoadAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SupportedExtensions_IncludesCommonFormats()
    {
        Assert.Contains(".jpg", _sut.SupportedExtensions);
        Assert.Contains(".png", _sut.SupportedExtensions);
        Assert.Contains(".bmp", _sut.SupportedExtensions);
        Assert.Contains(".gif", _sut.SupportedExtensions);
        Assert.Contains(".webp", _sut.SupportedExtensions);
    }
}
