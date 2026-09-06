using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SCANOVA.Imaging.Composition;
using SCANOVA.Imaging.ImageProcessing;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;
using Xunit;

namespace SCANOVA.Imaging.Tests;

public class DuplexCompositionServiceTests
{
    private readonly IDuplexCompositionService _sut = new DuplexCompositionService(new SkiaImageService());

    private static (byte R, byte G, byte B) PixelAt(RasterImage image, int x, int y)
    {
        var offset = y * image.Stride + x * 4;
        return (image.Pixels[offset], image.Pixels[offset + 1], image.Pixels[offset + 2]);
    }

    /// <summary>Imagem sólida com uma cor identificável — usada para checar identidade/ordem sem depender de conteúdo real.</summary>
    private static RasterImage CreateSolid(byte r, byte g, byte b, int size = 8)
    {
        var stride = size * 4;
        var pixels = new byte[stride * size];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = r;
            pixels[i + 1] = g;
            pixels[i + 2] = b;
            pixels[i + 3] = 255;
        }

        return new RasterImage(size, size, stride, CorePixelFormat.Rgba32, pixels, 200, 200);
    }

    /// <summary>Imagem com um bloco vermelho no canto superior esquerdo — para detectar rotação de 180°.</summary>
    private static RasterImage CreateMarkedImage(int width = 20, int height = 10)
    {
        var stride = width * 4;
        var pixels = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = y * stride + x * 4;
                var isMark = x < 4 && y < 4;
                pixels[offset] = isMark ? (byte)255 : (byte)0;
                pixels[offset + 1] = 0;
                pixels[offset + 2] = isMark ? (byte)0 : (byte)255;
                pixels[offset + 3] = 255;
            }
        }

        return new RasterImage(width, height, stride, CorePixelFormat.Rgba32, pixels, 200, 200);
    }

    [Fact]
    public void Compose_BasicCase_InterleavesFrontAndBackInOrder()
    {
        var fronts = new[] { CreateSolid(255, 0, 0), CreateSolid(0, 255, 0), CreateSolid(0, 0, 255) };
        var backs = new[] { CreateSolid(10, 10, 10), CreateSolid(20, 20, 20), CreateSolid(30, 30, 30) };

        var result = _sut.Compose(fronts, backs, new DuplexCompositionOptions { ReverseBackOrder = false });

        Assert.Equal(6, result.Count);
        Assert.Same(fronts[0], result[0]);
        Assert.Same(backs[0], result[1]);
        Assert.Same(fronts[1], result[2]);
        Assert.Same(backs[1], result[3]);
        Assert.Same(fronts[2], result[4]);
        Assert.Same(backs[2], result[5]);
    }

    [Fact]
    public void Compose_ReverseBackOrder_ReversesBacksBeforeInterleaving()
    {
        var fronts = new[] { CreateSolid(255, 0, 0), CreateSolid(0, 255, 0), CreateSolid(0, 0, 255) };
        var backs = new[] { CreateSolid(10, 10, 10), CreateSolid(20, 20, 20), CreateSolid(30, 30, 30) };

        var result = _sut.Compose(fronts, backs, new DuplexCompositionOptions { ReverseBackOrder = true });

        // Fluxo de duplex manual: a última página escaneada na frente é a primeira a sair no
        // verso — então backs[2] deve emparelhar com fronts[0], backs[0] com fronts[2].
        Assert.Same(fronts[0], result[0]);
        Assert.Same(backs[2], result[1]);
        Assert.Same(fronts[1], result[2]);
        Assert.Same(backs[1], result[3]);
        Assert.Same(fronts[2], result[4]);
        Assert.Same(backs[0], result[5]);
    }

    [Fact]
    public void Compose_RotateBackPages180_RotatesOnlyBackPages()
    {
        var front = CreateMarkedImage();
        var back = CreateMarkedImage();

        var result = _sut.Compose(new[] { front }, new[] { back }, new DuplexCompositionOptions { RotateBackPages180 = true });

        // A frente não deve ser alterada: o marcador continua no canto superior esquerdo.
        var (fr, _, _) = PixelAt(result[0], 1, 1);
        Assert.True(fr > 200);

        // O verso deve ter sido girado 180°: o marcador foi para o canto oposto.
        var (br, _, _) = PixelAt(result[1], result[1].Width - 2, result[1].Height - 2);
        Assert.True(br > 200, "Marcador do verso deveria estar no canto oposto após girar 180°.");

        // O canto original (topo-esquerda) do verso girado não deve mais ter o marcador.
        var (brOriginalCorner, _, _) = PixelAt(result[1], 1, 1);
        Assert.True(brOriginalCorner < 50);

        // A imagem original de entrada não deve ter sido modificada (não destrutivo).
        var (originalCorner, _, _) = PixelAt(back, 1, 1);
        Assert.True(originalCorner > 200);
    }

    [Fact]
    public void Compose_RotateBackPages180False_LeavesBackUnrotated()
    {
        var front = CreateMarkedImage();
        var back = CreateMarkedImage();

        var result = _sut.Compose(new[] { front }, new[] { back });

        Assert.Same(back, result[1]); // sem rotação, nem uma cópia é feita — mesma instância.
    }

    [Fact]
    public void Compose_SinglePage_Works()
    {
        var front = CreateSolid(255, 0, 0);
        var back = CreateSolid(0, 255, 0);

        var result = _sut.Compose(new[] { front }, new[] { back });

        Assert.Equal(2, result.Count);
        Assert.Same(front, result[0]);
        Assert.Same(back, result[1]);
    }

    [Fact]
    public void Compose_MismatchedCounts_Throws()
    {
        var fronts = new[] { CreateSolid(255, 0, 0), CreateSolid(0, 255, 0) };
        var backs = new[] { CreateSolid(0, 0, 255) };

        var ex = Assert.Throws<DuplexCompositionException>(() => _sut.Compose(fronts, backs));
        Assert.Contains("2", ex.UserMessage);
        Assert.Contains("1", ex.UserMessage);
    }

    [Fact]
    public void Compose_EmptyLists_Throws()
    {
        Assert.Throws<DuplexCompositionException>(() => _sut.Compose(Array.Empty<RasterImage>(), Array.Empty<RasterImage>()));
    }

    [Fact]
    public void Compose_NullOptions_BehavesLikeDefault()
    {
        var fronts = new[] { CreateSolid(255, 0, 0) };
        var backs = new[] { CreateSolid(0, 255, 0) };

        var withNull = _sut.Compose(fronts, backs, options: null);
        var withDefault = _sut.Compose(fronts, backs, DuplexCompositionOptions.Default);

        Assert.Same(withNull[0], withDefault[0]);
        Assert.Same(withNull[1], withDefault[1]);
    }

    [Fact]
    public void Compose_ReverseBackOrder_DoesNotMutateOriginalBackList()
    {
        var fronts = new[] { CreateSolid(255, 0, 0), CreateSolid(0, 255, 0) };
        var backs = new List<RasterImage> { CreateSolid(10, 10, 10), CreateSolid(20, 20, 20) };
        var originalOrder = backs.ToList();

        _sut.Compose(fronts, backs, new DuplexCompositionOptions { ReverseBackOrder = true });

        Assert.Equal(originalOrder, backs);
    }
}
