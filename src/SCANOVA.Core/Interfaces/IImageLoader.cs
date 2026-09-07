using SCANOVA.Core.Models;

namespace SCANOVA.Core.Interfaces;

/// <summary>
/// Carrega imagens de arquivo em memória como <see cref="RasterImage"/>. A arquitetura permite
/// adicionar novos formatos sem alterar os consumidores (seção 28).
/// </summary>
public interface IImageLoader
{
    /// <summary>Extensões de arquivo suportadas por este carregador (ex.: ".jpg", ".png"), em minúsculas, com o ponto.</summary>
    IReadOnlyCollection<string> SupportedExtensions { get; }

    /// <summary>Indica se este carregador consegue abrir o arquivo informado (checagem por conteúdo, não apenas extensão — seção 64).</summary>
    Task<bool> CanLoadAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>Carrega o arquivo em memória. Lança <see cref="Exceptions.ImageLoadException"/> em caso de falha.</summary>
    Task<RasterImage> LoadAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>Carrega a partir de um stream já aberto (ex.: arquivo arrastado, buffer de digitalização).</summary>
    Task<RasterImage> LoadAsync(Stream stream, CancellationToken cancellationToken = default);
}
