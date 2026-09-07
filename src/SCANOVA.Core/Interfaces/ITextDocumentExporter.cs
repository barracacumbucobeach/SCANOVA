namespace SCANOVA.Core.Interfaces;

/// <summary>
/// Exporta texto (tipicamente o resultado de um OCR, seção 111) para um arquivo de texto simples
/// (TXT) ou para um documento do Word (DOCX).
/// </summary>
public interface ITextDocumentExporter
{
    Task SaveAsTextAsync(string text, string filePath, CancellationToken cancellationToken = default);

    /// <summary>Gera um DOCX simples: um parágrafo por linha do texto (quebras de linha preservadas).</summary>
    Task SaveAsDocxAsync(string text, string filePath, CancellationToken cancellationToken = default);
}
