using SCANOVA.Core.Enums;

namespace SCANOVA.Core.Models;

/// <summary>
/// Representação de uma imagem rasterizada em memória, independente de qualquer biblioteca
/// de imagem específica. É o tipo trafegado entre <c>SCANOVA.Imaging</c>, <c>SCANOVA.Tiff</c>
/// e <c>SCANOVA.Pdf</c>, mantendo o domínio (<c>SCANOVA.Core</c>) livre de dependências de terceiros.
/// </summary>
public sealed class RasterImage
{
    /// <summary>Largura em pixels.</summary>
    public int Width { get; }

    /// <summary>Altura em pixels.</summary>
    public int Height { get; }

    /// <summary>Número de bytes por linha (pode incluir preenchimento/alinhamento).</summary>
    public int Stride { get; }

    /// <summary>Formato do pixel armazenado em <see cref="Pixels"/>.</summary>
    public PixelFormat Format { get; }

    /// <summary>Resolução horizontal em pontos por polegada (DPI).</summary>
    public double HorizontalDpi { get; init; }

    /// <summary>Resolução vertical em pontos por polegada (DPI).</summary>
    public double VerticalDpi { get; init; }

    /// <summary>Buffer de pixels bruto, no formato indicado por <see cref="Format"/>.</summary>
    public byte[] Pixels { get; }

    public RasterImage(int width, int height, int stride, PixelFormat format, byte[] pixels, double horizontalDpi = 200, double verticalDpi = 200)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (stride <= 0) throw new ArgumentOutOfRangeException(nameof(stride));
        ArgumentNullException.ThrowIfNull(pixels);

        var minStride = MinimumStride(width, format);
        if (stride < minStride)
        {
            throw new ArgumentException(
                $"Stride ({stride}) menor que o mínimo necessário ({minStride}) para largura {width} no formato {format}.",
                nameof(stride));
        }

        var expectedLength = (long)stride * height;
        if (pixels.LongLength < expectedLength)
        {
            throw new ArgumentException(
                $"Buffer de pixels tem {pixels.LongLength} bytes, mas são necessários pelo menos {expectedLength} (stride × height).",
                nameof(pixels));
        }

        Width = width;
        Height = height;
        Stride = stride;
        Format = format;
        Pixels = pixels;
        HorizontalDpi = horizontalDpi;
        VerticalDpi = verticalDpi;
    }

    /// <summary>Bytes por pixel (aproximado; não se aplica a <see cref="PixelFormat.Bilevel1"/>, que é empacotado por bit).</summary>
    public static int BytesPerPixel(PixelFormat format) => format switch
    {
        PixelFormat.Gray8 => 1,
        PixelFormat.Rgb24 => 3,
        PixelFormat.Rgba32 => 4,
        PixelFormat.Bilevel1 => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    /// <summary>Calcula o stride mínimo (sem preenchimento extra) para a largura e formato informados.</summary>
    public static int MinimumStride(int width, PixelFormat format) => format switch
    {
        PixelFormat.Bilevel1 => (width + 7) / 8,
        _ => width * BytesPerPixel(format),
    };
}
