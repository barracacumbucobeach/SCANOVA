using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Imaging.ImageLoading;
using Xunit;

namespace SCANOVA.Imaging.Tests;

public class SkiaImageExporterTests
{
    private readonly IImageExporter _sut = new SkiaImageExporter();

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    public async Task SaveAsync_WritesNonEmptyFile(string extension)
    {
        var image = TestImages.CreateDocumentLike(64, 48);
        var path = Path.Combine(Path.GetTempPath(), $"scanova-export-{Guid.NewGuid():N}{extension}");

        try
        {
            await _sut.SaveAsync(image, path);

            var info = new FileInfo(path);
            Assert.True(info.Exists);
            Assert.True(info.Length > 0);
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
    public async Task SaveAsync_CreatesDestinationDirectory()
    {
        var image = TestImages.CreateSolid(10, 10, 100, 100, 100);
        var dir = Path.Combine(Path.GetTempPath(), $"scanova-dir-{Guid.NewGuid():N}");
        var path = Path.Combine(dir, "out.png");

        try
        {
            await _sut.SaveAsync(image, path);
            Assert.True(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SaveAsync_UnsupportedExtension_ThrowsImageLoadException()
    {
        var image = TestImages.CreateSolid(10, 10, 1, 2, 3);
        var path = Path.Combine(Path.GetTempPath(), $"scanova-bad-{Guid.NewGuid():N}.gif");

        await Assert.ThrowsAsync<ImageLoadException>(() => _sut.SaveAsync(image, path));
    }
}
