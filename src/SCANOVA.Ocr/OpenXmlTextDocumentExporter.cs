using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;

namespace SCANOVA.Ocr;

/// <summary>
/// Implementação de <see cref="ITextDocumentExporter"/>: TXT como texto simples (UTF-8), DOCX
/// via DocumentFormat.OpenXml (MIT) — um parágrafo por linha, preservando as quebras de linha do
/// texto reconhecido (seção 111).
/// </summary>
public sealed class OpenXmlTextDocumentExporter : ITextDocumentExporter
{
    public async Task SaveAsTextAsync(string text, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        cancellationToken.ThrowIfCancellationRequested();
        EnsureDirectoryExists(filePath);

        try
        {
            await File.WriteAllTextAsync(filePath, text, System.Text.Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new FileAccessException(
                "Não foi possível salvar o arquivo de texto.",
                $"Falha ao gravar TXT em \"{filePath}\": {ex.Message}",
                ex);
        }
    }

    public Task SaveAsDocxAsync(string text, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        cancellationToken.ThrowIfCancellationRequested();
        EnsureDirectoryExists(filePath);

        return Task.Run(() =>
        {
            try
            {
                using var document = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
                var mainPart = document.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = mainPart.Document.AppendChild(new Body());

                foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var run = new Run(new Text(line) { Space = SpaceProcessingModeValues.Preserve });
                    body.AppendChild(new Paragraph(run));
                }

                mainPart.Document.Save();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new FileAccessException(
                    "Não foi possível salvar o documento do Word.",
                    $"Falha ao gravar DOCX em \"{filePath}\": {ex.Message}",
                    ex);
            }
        }, cancellationToken);
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
