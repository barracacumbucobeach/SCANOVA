using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SCANOVA.Imaging.ImageProcessing;
using SkiaSharp;
using PixelFormat = SCANOVA.Core.Enums.PixelFormat;

namespace SCANOVA.Imaging.ImageLoading;

/// <summary>
/// Grava um <see cref="RasterImage"/> como PNG ou JPG usando SkiaSharp. Note que o Skia não
/// possui encoder de BMP (apenas decoder) — por isso BMP não está entre os formatos de
/// exportação aqui, o que é consistente com o menu "Salvar como" da especificação (seção 32),
/// que também não lista BMP como destino (BMP só é suportado como formato de abertura —
/// seção 28).
/// </summary>
public sealed class SkiaImageExporter : IImageExporter
{
    public IReadOnlyCollection<string> SupportedExtensions { get; } = new[] { ".png", ".jpg", ".jpeg" };

    public Task SaveAsync(RasterImage image, string filePath, int quality = 90, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var format = extension switch
        {
            ".png" => SKEncodedImageFormat.Png,
            ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
            _ => throw new ImageLoadException(
                $"Formato de exportação \"{extension}\" não é suportado.",
                $"Extensão não suportada por {nameof(SkiaImageExporter)}: {extension}"),
        };

        // Seção 7/61: codificação/gravação em disco não deve rodar na UI thread.
        return Task.Run(() =>
        {
            using var bitmap = SkiaConversions.ToSkBitmap(image);
            using var skImage = SKImage.FromBitmap(bitmap);
            using var encoded = skImage.Encode(format, Math.Clamp(quality, 1, 100));

            if (encoded is null)
            {
                throw new ImageLoadException(
                    "Não foi possível gerar o arquivo de imagem.",
                    $"SKImage.Encode retornou null para o formato {format}.");
            }

            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var fileStream = File.Create(filePath);
                encoded.SaveTo(fileStream);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new FileAccessException(
                    "Não foi possível salvar o arquivo. Verifique se a pasta está disponível e tente novamente.",
                    $"Acesso negado ao salvar: {filePath}", ex);
            }
            catch (DirectoryNotFoundException ex)
            {
                throw new FileAccessException(
                    "A pasta de destino não foi encontrada.",
                    $"Diretório inexistente: {filePath}", ex);
            }
            catch (IOException ex)
            {
                throw new FileAccessException(
                    "Não foi possível salvar o arquivo. Verifique o espaço em disco disponível.",
                    $"Erro de E/S ao salvar: {filePath}", ex);
            }
        }, cancellationToken);
    }
}
