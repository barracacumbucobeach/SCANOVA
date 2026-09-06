using SCANOVA.Core.Enums;
using SCANOVA.Core.Models;
using Xunit;

namespace SCANOVA.Core.Tests;

public class RasterImageTests
{
    [Fact]
    public void Constructor_ValidBuffer_Succeeds()
    {
        var pixels = new byte[1600]; // 20x20 Rgba32 -> stride 80 × 20 linhas
        var image = new RasterImage(20, 20, 80, PixelFormat.Rgba32, pixels, 200, 200);

        Assert.Equal(20, image.Width);
        Assert.Equal(20, image.Height);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-1, 10)]
    public void Constructor_InvalidDimensions_Throws(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RasterImage(width, height, Math.Max(width, 1) * 4, PixelFormat.Rgba32, new byte[4000], 200, 200));
    }

    [Fact]
    public void Constructor_StrideSmallerThanMinimum_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new RasterImage(20, 20, 10, PixelFormat.Rgba32, new byte[4000], 200, 200)); // precisa de stride >= 80
    }

    [Fact]
    public void Constructor_BufferSmallerThanStrideTimesHeight_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new RasterImage(20, 20, 80, PixelFormat.Rgba32, new byte[100], 200, 200));
    }

    [Theory]
    [InlineData(PixelFormat.Gray8, 1)]
    [InlineData(PixelFormat.Rgb24, 3)]
    [InlineData(PixelFormat.Rgba32, 4)]
    public void BytesPerPixel_MatchesFormat(PixelFormat format, int expected)
    {
        Assert.Equal(expected, RasterImage.BytesPerPixel(format));
    }

    [Theory]
    [InlineData(8, 1)]
    [InlineData(9, 2)]
    [InlineData(16, 2)]
    [InlineData(17, 3)]
    public void MinimumStride_Bilevel1_RoundsUpToWholeBytes(int width, int expectedStride)
    {
        Assert.Equal(expectedStride, RasterImage.MinimumStride(width, PixelFormat.Bilevel1));
    }
}
