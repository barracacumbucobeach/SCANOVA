using SCANOVA.Core.Models;

namespace SCANOVA.Core.Interfaces;

/// <summary>
/// Codifica uma ou mais <see cref="RasterImage"/> em um arquivo TIFF, respeitando a
/// <see cref="TiffSettings"/> informada (resolução, modo de cor, compressão). A implementação
/// não deve nunca simplesmente renomear/reextensionar um arquivo (seção 23): quando a
/// configuração pedir CCITT Group 4, a imagem de entrada precisa estar (ou ser convertida para)
/// 1 bit antes da codificação.
/// </summary>
public interface ITiffEncoder
{
    /// <summary>Codifica uma única página.</summary>
    Task EncodeAsync(RasterImage image, TiffSettings settings, string filePath, CancellationToken cancellationToken = default);

    /// <summary>Codifica múltiplas páginas em um único arquivo TIFF multipágina.</summary>
    Task EncodeMultiPageAsync(IReadOnlyList<RasterImage> pages, TiffSettings settings, string filePath, CancellationToken cancellationToken = default);

    /// <summary>Decodifica um arquivo TIFF, retornando cada página como um <see cref="RasterImage"/> independente.</summary>
    Task<IReadOnlyList<RasterImage>> DecodeAsync(string filePath, CancellationToken cancellationToken = default);
}
