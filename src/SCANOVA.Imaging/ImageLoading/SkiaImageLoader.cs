using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SkiaSharp;
using PixelFormat = SCANOVA.Core.Enums.PixelFormat;

namespace SCANOVA.Imaging.ImageLoading;

/// <summary>
/// Carrega JPG/JPEG/PNG/BMP/GIF/WEBP usando SkiaSharp (seção 28). TIFF é tratado por
/// <c>SCANOVA.Tiff</c>, que precisa de controle fino sobre bilevel/CCITT não oferecido pelo
/// decodificador TIFF embutido do Skia.
/// </summary>
public sealed class SkiaImageLoader : IImageLoader
{
    /// <summary>DPI assumido quando o arquivo de origem não carrega metadado de resolução confiável.</summary>
    public const double DefaultAssumedDpi = 200;

    public IReadOnlyCollection<string> SupportedExtensions { get; } =
        new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };

    public Task<bool> CanLoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(filePath))
        {
            return Task.FromResult(false);
        }

        // Seção 7/61: mesmo essa checagem leve não deve rodar na UI thread quando chamada a
        // partir dela — despacha para o thread pool.
        return Task.Run(() =>
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                using var codec = SKCodec.Create(stream);
                return codec is not null;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }

    public async Task<RasterImage> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new ImageLoadException(
                "O arquivo não foi encontrado.",
                $"Arquivo inexistente: {filePath}");
        }

        Stream stream;
        try
        {
            stream = File.OpenRead(filePath);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ImageLoadException(
                "Não foi possível abrir o arquivo. Verifique as permissões da pasta.",
                $"Acesso negado: {filePath}", ex);
        }
        catch (IOException ex)
        {
            throw new ImageLoadException(
                "Não foi possível abrir o arquivo. Ele pode estar em uso por outro programa.",
                $"Erro de E/S ao abrir: {filePath}", ex);
        }

        using (stream)
        {
            return await LoadAsync(stream, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task<RasterImage> LoadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Seção 7/61: decodificação é potencialmente pesada para imagens grandes de scanner —
        // nunca deve rodar na UI thread. Despacha o trabalho síncrono do Skia para o thread pool.
        return Task.Run(() =>
        {
            using var data = SKData.Create(stream);
            using var codec = SKCodec.Create(data);
            if (codec is null)
            {
                throw new ImageLoadException(
                    "O arquivo não é uma imagem válida ou o formato não é suportado.",
                    "SKCodec.Create retornou null — formato não reconhecido ou dados corrompidos.");
            }

            using var bitmap = SKBitmap.Decode(codec);
            if (bitmap is null)
            {
                throw new ImageLoadException(
                    "Não foi possível decodificar a imagem. O arquivo pode estar corrompido.",
                    "SKBitmap.Decode retornou null.");
            }

            return ConvertToRasterImage(bitmap);
        }, cancellationToken);
    }

    private static RasterImage ConvertToRasterImage(SKBitmap bitmap)
    {
        // Nunca descarta `bitmap` aqui — ele pertence ao chamador. Só a cópia eventualmente
        // criada para normalizar o color type é descartada localmente.
        if (bitmap.ColorType == SKColorType.Rgba8888)
        {
            return CopyPixels(bitmap);
        }

        using var converted = bitmap.Copy(SKColorType.Rgba8888);
        if (converted is null)
        {
            throw new ImageLoadException(
                "Não foi possível converter a imagem decodificada.",
                "SKBitmap.Copy(Rgba8888) retornou null.");
        }

        return CopyPixels(converted);
    }

    private static RasterImage CopyPixels(SKBitmap rgba)
    {
        var stride = rgba.RowBytes;
        var pixels = new byte[stride * rgba.Height];
        System.Runtime.InteropServices.Marshal.Copy(rgba.GetPixels(), pixels, 0, pixels.Length);

        return new RasterImage(rgba.Width, rgba.Height, stride, PixelFormat.Rgba32, pixels, DefaultAssumedDpi, DefaultAssumedDpi);
    }
}
