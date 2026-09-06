using SCANOVA.Core.Models;

namespace SCANOVA.Core.Interfaces;

/// <summary>Leitura e geração de arquivos PDF (imagem-only, colorido, escala de cinza ou pesquisável).</summary>
public interface IPdfService
{
    /// <summary>Gera um PDF a partir de uma ou mais páginas já processadas.</summary>
    Task WritePdfAsync(IReadOnlyList<RasterImage> pages, PdfSettings settings, string filePath, IReadOnlyList<string>? ocrTextPerPage = null, CancellationToken cancellationToken = default);

    /// <summary>Converte um TIFF (possivelmente multipágina) para PDF, preservando todas as páginas (seção 31).</summary>
    Task ConvertTiffToPdfAsync(string tiffPath, PdfSettings settings, string outputPdfPath, CancellationToken cancellationToken = default);

    /// <summary>Tenta extrair texto nativo do PDF (quando já contém texto), sem OCR (seção 109).</summary>
    Task<string?> TryExtractTextAsync(string pdfPath, int pageIndex, CancellationToken cancellationToken = default);
}
