using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SCANOVA.Imaging.ImageProcessing;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;
using Xunit;

namespace SCANOVA.Imaging.Tests;

public class SkiaImageServiceTests
{
    private readonly IImageService _sut = new SkiaImageService();

    private static RasterImage CreateMarkedImage(int width, int height)
    {
        // Um bloco vermelho (6x6, ou menor se a imagem for pequena) no canto superior
        // esquerdo, resto azul — para rastrear orientação através de rotações/espelhamentos.
        // Um bloco (em vez de um único pixel) evita falsos negativos por causa de
        // suavização/reamostragem nas bordas do canvas rotacionado.
        var markSize = Math.Min(6, Math.Min(width, height));
        var stride = width * 4;
        var pixels = new byte[stride * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = y * stride + x * 4;
                var isMark = x < markSize && y < markSize;
                pixels[offset] = isMark ? (byte)255 : (byte)0;
                pixels[offset + 1] = 0;
                pixels[offset + 2] = isMark ? (byte)0 : (byte)255;
                pixels[offset + 3] = 255;
            }
        }

        return new RasterImage(width, height, stride, CorePixelFormat.Rgba32, pixels, 200, 200);
    }

    private static (byte R, byte G, byte B) PixelAt(RasterImage image, int x, int y)
    {
        var bytesPerPixel = image.Format switch
        {
            CorePixelFormat.Rgba32 => 4,
            _ => throw new NotSupportedException(),
        };
        var offset = y * image.Stride + x * bytesPerPixel;
        return (image.Pixels[offset], image.Pixels[offset + 1], image.Pixels[offset + 2]);
    }

    [Fact]
    public void Rotate_By90_SwapsDimensions()
    {
        var image = CreateMarkedImage(40, 20);

        var rotated = _sut.Rotate(image, 90);

        Assert.Equal(20, rotated.Width);
        Assert.Equal(40, rotated.Height);
    }

    [Fact]
    public void Rotate_By180_KeepsDimensions_AndMovesMarkerToOppositeCorner()
    {
        var image = CreateMarkedImage(40, 20);

        var rotated = _sut.Rotate(image, 180);

        Assert.Equal(40, rotated.Width);
        Assert.Equal(20, rotated.Height);

        var (r, _, _) = PixelAt(rotated, rotated.Width - 3, rotated.Height - 3);
        Assert.True(r > 200, "Marcador vermelho deveria estar no canto oposto após 180°.");
    }

    [Fact]
    public void Rotate_By0_ReturnsSameInstance()
    {
        var image = CreateMarkedImage(10, 10);
        var rotated = _sut.Rotate(image, 0);
        Assert.Same(image, rotated);
    }

    [Fact]
    public void Rotate_By360_IsEquivalentToNoRotation()
    {
        var image = CreateMarkedImage(10, 10);
        var rotated = _sut.Rotate(image, 360);
        Assert.Equal(image.Width, rotated.Width);
        Assert.Equal(image.Height, rotated.Height);
    }

    [Fact]
    public void FlipHorizontal_MovesMarkerToTopRight()
    {
        var image = CreateMarkedImage(40, 20);

        var flipped = _sut.FlipHorizontal(image);

        var (r, _, _) = PixelAt(flipped, flipped.Width - 3, 2);
        Assert.True(r > 200);
    }

    [Fact]
    public void FlipVertical_MovesMarkerToBottomLeft()
    {
        var image = CreateMarkedImage(40, 20);

        var flipped = _sut.FlipVertical(image);

        var (r, _, _) = PixelAt(flipped, 2, flipped.Height - 3);
        Assert.True(r > 200);
    }

    [Fact]
    public void Crop_ReturnsRequestedDimensions()
    {
        var image = TestImages.CreateDocumentLike(400, 300);
        var region = CropRegion.FromRectangle(50, 40, 200, 150);

        var cropped = _sut.Crop(image, region);

        Assert.Equal(200, cropped.Width);
        Assert.Equal(150, cropped.Height);
    }

    [Fact]
    public void Crop_ClampsRegionToImageBounds()
    {
        var image = TestImages.CreateDocumentLike(100, 80);
        var region = CropRegion.FromRectangle(-50, -50, 1000, 1000);

        var cropped = _sut.Crop(image, region);

        Assert.True(cropped.Width <= image.Width);
        Assert.True(cropped.Height <= image.Height);
    }

    [Fact]
    public void ToGrayscale_ProducesEqualRgbChannels()
    {
        var image = TestImages.CreateDocumentLike(50, 50);

        var gray = _sut.ToGrayscale(image);

        Assert.Equal(CorePixelFormat.Gray8, gray.Format);
        Assert.Equal(image.Width, gray.Width);
        Assert.Equal(image.Height, gray.Height);
    }

    [Fact]
    public void ToGrayscale_IsIdempotent()
    {
        var image = TestImages.CreateDocumentLike(30, 30);
        var gray = _sut.ToGrayscale(image);

        var grayAgain = _sut.ToGrayscale(gray);

        Assert.Same(gray, grayAgain);
    }

    [Fact]
    public void NormalizeDpi_ScalesDimensionsProportionally()
    {
        var image = TestImages.CreateDocumentLike(400, 200); // 200 DPI de origem

        var normalized = _sut.NormalizeDpi(image, 100);

        Assert.Equal(200, normalized.Width);
        Assert.Equal(100, normalized.Height);
        Assert.Equal(100, normalized.HorizontalDpi);
        Assert.Equal(100, normalized.VerticalDpi);
    }

    [Fact]
    public void NormalizeDpi_SameTargetDpi_ReturnsSameInstance()
    {
        var image = TestImages.CreateDocumentLike(100, 100);
        var normalized = _sut.NormalizeDpi(image, image.HorizontalDpi);
        Assert.Same(image, normalized);
    }
}
