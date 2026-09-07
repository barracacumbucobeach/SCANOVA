using SCANOVA.Core.Models;

namespace SCANOVA.Core.Interfaces;

/// <summary>Leitura e geração de arquivos PDF (imagem-only, colorido, escala de cinza ou pesquisável).</summary>
public interface IPdfService
{
    /// <summary>
    /// Gera um PDF a partir de uma ou mais páginas já processadas. Quando
    /// <paramref name="ocrResults"/> é informado (um item por página, na mesma ordem de
    /// <paramref name="pages"/>) e <see cref="PdfSettings.Mode"/> é
    /// <see cref="Enums.PdfMode.Searchable"/>, cada bloco de texto reconhecido
    /// (<see cref="OcrBlock"/>) vira uma camada de texto invisível posicionada sobre a imagem,
    /// na posição correspondente (seção 109-111) — o PDF passa a ser pesquisável/selecionável
    /// sem alterar a aparência visual da página.
    /// </summary>
    Task WritePdfAsync(IReadOnlyList<RasterImage> pages, PdfSettings settings, string filePath, IReadOnlyList<OcrResult>? ocrResults = null, CancellationToken cancellationToken = default);

    /// <summary>Converte um TIFF (possivelmente multipágina) para PDF, preservando todas as páginas (seção 31).</summary>
    Task ConvertTiffToPdfAsync(string tiffPath, PdfSettings settings, string outputPdfPath, CancellationToken cancellationToken = default);

    /// <summary>Tenta extrair texto nativo do PDF (quando já contém texto), sem OCR (seção 109).</summary>
    Task<string?> TryExtractTextAsync(string pdfPath, int pageIndex, CancellationToken cancellationToken = default);
}
