using SCANOVA.Core.Models;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;

namespace SCANOVA.Imaging.Enhancement;

/// <summary>
/// Remove pequenas manchas isoladas de uma imagem já binarizada (seção 19: "remoção de pequenas
/// manchas"): agrupa pixels pretos conectados (8-conectividade, adequada para traços de
/// tinta/caligrafia, que frequentemente se tocam apenas na diagonal) e apaga (torna branco)
/// qualquer grupo com área menor que <paramref name="minAreaPixels"/> em <see cref="Apply"/> —
/// tipicamente poeira/ruído da digitalização, não conteúdo real do documento. Passo final do
/// pipeline de binarização em <c>DocumentEnhancementService</c>.
/// </summary>
internal static class BilevelDespeckle
{
    public static RasterImage Apply(RasterImage bilevelImage, int minAreaPixels = 3)
    {
        if (bilevelImage.Format != CorePixelFormat.Bilevel1)
        {
            throw new ArgumentException("BilevelDespeckle requer uma imagem Bilevel1.", nameof(bilevelImage));
        }

        var width = bilevelImage.Width;
        var height = bilevelImage.Height;
        var black = Unpack(bilevelImage);

        var visited = new bool[width * height];
        var component = new List<int>();
        var queue = new Queue<int>();

        for (var start = 0; start < black.Length; start++)
        {
            if (!black[start] || visited[start])
            {
                continue;
            }

            component.Clear();
            queue.Clear();
            queue.Enqueue(start);
            visited[start] = true;

            while (queue.Count > 0)
            {
                var index = queue.Dequeue();
                component.Add(index);
                var x = index % width;
                var y = index / width;

                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        var nx = x + dx;
                        var ny = y + dy;
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;

                        var neighborIndex = ny * width + nx;
                        if (black[neighborIndex] && !visited[neighborIndex])
                        {
                            visited[neighborIndex] = true;
                            queue.Enqueue(neighborIndex);
                        }
                    }
                }
            }

            if (component.Count < minAreaPixels)
            {
                foreach (var index in component)
                {
                    black[index] = false;
                }
            }
        }

        return Pack(black, width, height, bilevelImage.HorizontalDpi, bilevelImage.VerticalDpi);
    }

    private static bool[] Unpack(RasterImage image)
    {
        var result = new bool[image.Width * image.Height];
        for (var y = 0; y < image.Height; y++)
        {
            var rowOffset = y * image.Stride;
            for (var x = 0; x < image.Width; x++)
            {
                var byteIndex = rowOffset + x / 8;
                var bitIndex = 7 - x % 8;
                result[y * image.Width + x] = ((image.Pixels[byteIndex] >> bitIndex) & 1) == 1;
            }
        }

        return result;
    }

    private static RasterImage Pack(bool[] black, int width, int height, double hDpi, double vDpi)
    {
        var stride = RasterImage.MinimumStride(width, CorePixelFormat.Bilevel1);
        var pixels = new byte[stride * height];

        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * stride;
            for (var x = 0; x < width; x++)
            {
                if (!black[y * width + x])
                {
                    continue;
                }

                var byteIndex = rowOffset + x / 8;
                var bitIndex = 7 - x % 8;
                pixels[byteIndex] |= (byte)(1 << bitIndex);
            }
        }

        return new RasterImage(width, height, stride, CorePixelFormat.Bilevel1, pixels, hDpi, vDpi);
    }
}
