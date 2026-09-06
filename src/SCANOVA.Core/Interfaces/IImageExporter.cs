using SCANOVA.Core.Models;

namespace SCANOVA.Core.Interfaces;

/// <summary>Grava um <see cref="RasterImage"/> em disco em um formato de imagem simples (PNG/JPG/BMP...).</summary>
public interface IImageExporter
{
    IReadOnlyCollection<string> SupportedExtensions { get; }

    Task SaveAsync(RasterImage image, string filePath, int quality = 90, CancellationToken cancellationToken = default);
}
