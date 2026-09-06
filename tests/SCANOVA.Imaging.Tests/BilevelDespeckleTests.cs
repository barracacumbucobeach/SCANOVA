using SCANOVA.Core.Models;
using SCANOVA.Imaging.Enhancement;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;
using Xunit;

namespace SCANOVA.Imaging.Tests;

public class BilevelDespeckleTests
{
    private static bool GetBit(RasterImage image, int x, int y)
    {
        var byteIndex = y * image.Stride + x / 8;
        var bitIndex = 7 - x % 8;
        return ((image.Pixels[byteIndex] >> bitIndex) & 1) == 1;
    }

    private static RasterImage CreateBilevel(int width, int height, Action<byte[], int> setBlack)
    {
        var stride = RasterImage.MinimumStride(width, CorePixelFormat.Bilevel1);
        var pixels = new byte[stride * height];
        setBlack(pixels, stride);
        return new RasterImage(width, height, stride, CorePixelFormat.Bilevel1, pixels, 200, 200);
    }

    [Fact]
    public void Apply_IsolatedSinglePixelSpeckle_IsRemoved()
    {
        var image = CreateBilevel(40, 40, (pixels, stride) =>
        {
            // Um único pixel preto isolado em (20,20).
            var byteIndex = 20 * stride + 20 / 8;
            var bitIndex = 7 - 20 % 8;
            pixels[byteIndex] |= (byte)(1 << bitIndex);
        });

        var result = BilevelDespeckle.Apply(image, minAreaPixels: 3);

        Assert.False(GetBit(result, 20, 20));
    }

    [Fact]
    public void Apply_LargeBlackRegion_IsPreserved()
    {
        var image = CreateBilevel(40, 40, (pixels, stride) =>
        {
            for (var y = 5; y < 35; y++)
            {
                for (var x = 5; x < 35; x++)
                {
                    var byteIndex = y * stride + x / 8;
                    var bitIndex = 7 - x % 8;
                    pixels[byteIndex] |= (byte)(1 << bitIndex);
                }
            }
        });

        var result = BilevelDespeckle.Apply(image, minAreaPixels: 3);

        Assert.True(GetBit(result, 20, 20));
        Assert.True(GetBit(result, 6, 6));
    }

    [Fact]
    public void Apply_DiagonallyConnectedPixels_CountAsOneComponentAndSurviveTogether()
    {
        // Uma "escada" diagonal de 4 pixels só se toca por conectividade-8 — deve ser tratada
        // como um único componente de 4 pixels e sobreviver a um limiar de 3.
        var image = CreateBilevel(40, 40, (pixels, stride) =>
        {
            void SetPixel(int x, int y)
            {
                var byteIndex = y * stride + x / 8;
                var bitIndex = 7 - x % 8;
                pixels[byteIndex] |= (byte)(1 << bitIndex);
            }

            SetPixel(10, 10);
            SetPixel(11, 11);
            SetPixel(12, 12);
            SetPixel(13, 13);
        });

        var result = BilevelDespeckle.Apply(image, minAreaPixels: 3);

        Assert.True(GetBit(result, 10, 10));
        Assert.True(GetBit(result, 13, 13));
    }
}
