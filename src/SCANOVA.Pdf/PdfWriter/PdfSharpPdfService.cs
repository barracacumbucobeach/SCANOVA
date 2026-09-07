using PdfSharp.Drawing;
using PdfSharp.Pdf;
using SCANOVA.Core.Enums;
using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SkiaSharp;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;

namespace SCANOVA.Pdf.PdfWriter;

/// <summary>
/// Implementação de <see cref="IPdfService"/> usando PDFsharp (MIT) para escrita e PdfPig
/// (Apache 2.0) para extração de texto nativo. Gera PDFs "imagem-only" (cada página é uma
/// imagem PNG/JPEG embutida, do tamanho físico exato da página, calculado a partir do DPI da
/// imagem) — o mesmo modelo usado pela maioria dos aplicativos de digitalização. Quando
/// <see cref="PdfMode.Searchable"/>/<see cref="PdfSettings.IncludeOcrTextLayer"/> é pedido junto
/// de <c>ocrResults</c>, cada bloco reconhecido pelo OCR (Fase 9) vira uma camada de texto
/// invisível posicionada sobre a imagem — ver <see cref="EmbeddedFontResolver"/> e
/// <c>docs/PDF.md</c>.
/// </summary>
public sealed class PdfSharpPdfService : IPdfService
{
    private readonly ITiffEncoder _tiffEncoder;

    public PdfSharpPdfService(ITiffEncoder tiffEncoder)
    {
        _tiffEncoder = tiffEncoder;
        EmbeddedFontResolver.EnsureRegistered();
    }

    public Task WritePdfAsync(
        IReadOnlyList<RasterImage> pages,
        PdfSettings settings,
        string filePath,
        IReadOnlyList<OcrResult>? ocrResults = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (pages.Count == 0)
        {
            throw new PdfProcessingException(
                "Não há páginas para salvar no PDF.",
                $"{nameof(WritePdfAsync)} chamado com uma lista de páginas vazia.");
        }

        if (ocrResults is not null && ocrResults.Count != pages.Count)
        {
            throw new PdfProcessingException(
                "A lista de resultados de OCR precisa ter uma entrada para cada página.",
                $"{nameof(pages)}.Count={pages.Count}, {nameof(ocrResults)}.Count={ocrResults.Count}.");
        }

        var includeTextLayer = settings.Mode == PdfMode.Searchable || settings.IncludeOcrTextLayer;
        if (includeTextLayer && ocrResults is null)
        {
            throw new PdfProcessingException(
                "PDF pesquisável exige o resultado do reconhecimento de texto (OCR) de cada página.",
                $"{nameof(PdfMode.Searchable)}/{nameof(PdfSettings.IncludeOcrTextLayer)} pedido sem {nameof(ocrResults)}.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() =>
        {
            try
            {
                using var document = new PdfDocument
                {
                    Info = { Creator = "SCANOVA", Title = Path.GetFileNameWithoutExtension(filePath) },
                };

                for (var i = 0; i < pages.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var ocrResult = includeTextLayer ? ocrResults![i] : null;
                    AddImagePage(document, pages[i], settings, ocrResult);
                }

                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                document.Save(filePath);
            }
            catch (ScanovaException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PdfProcessingException(
                    "Não foi possível gerar o arquivo PDF.",
                    $"Falha ao gerar PDF em \"{filePath}\": {ex.Message}",
                    ex);
            }
        }, cancellationToken);
    }

    public async Task ConvertTiffToPdfAsync(
        string tiffPath,
        PdfSettings settings,
        string outputPdfPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tiffPath);

        if (!File.Exists(tiffPath))
        {
            throw new PdfProcessingException(
                "O arquivo TIFF não foi encontrado.",
                $"Arquivo TIFF inexistente: {tiffPath}");
        }

        // Seção 31: todas as páginas do TIFF (multipágina ou não) viram páginas do PDF, na ordem.
        var pages = await _tiffEncoder.DecodeAsync(tiffPath, cancellationToken).ConfigureAwait(false);

        await WritePdfAsync(pages, settings, outputPdfPath, ocrResults: null, cancellationToken).ConfigureAwait(false);
    }

    public Task<string?> TryExtractTextAsync(string pdfPath, int pageIndex, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfPath);

        if (!File.Exists(pdfPath))
        {
            throw new PdfProcessingException(
                "O arquivo PDF não foi encontrado.",
                $"Arquivo PDF inexistente: {pdfPath}");
        }

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() =>
        {
            try
            {
                using var document = UglyToad.PdfPig.PdfDocument.Open(pdfPath);

                if (pageIndex < 0 || pageIndex >= document.NumberOfPages)
                {
                    throw new PdfProcessingException(
                        "A página solicitada não existe neste PDF.",
                        $"Página {pageIndex} solicitada, mas o PDF tem {document.NumberOfPages} página(s).");
                }

                // PdfPig numera páginas a partir de 1; o restante do SCANOVA usa índice 0-based.
                var text = document.GetPage(pageIndex + 1).Text;

                // Sem texto nativo (PDF imagem-only, o caso comum de um documento digitalizado) —
                // retorna null em vez de string vazia, para o chamador distinguir "sem texto" de
                // "OCR ainda não rodou" (seção 109).
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
            catch (ScanovaException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PdfProcessingException(
                    "Não foi possível ler o texto do PDF.",
                    $"Falha ao extrair texto de \"{pdfPath}\" (página {pageIndex}): {ex.Message}",
                    ex);
            }
        }, cancellationToken);
    }

    private static void AddImagePage(PdfDocument document, RasterImage image, PdfSettings settings, OcrResult? ocrResult)
    {
        ValidatePageAgainstMode(image, settings.Mode);

        var dpiX = image.HorizontalDpi > 0 ? image.HorizontalDpi : settings.Dpi;
        var dpiY = image.VerticalDpi > 0 ? image.VerticalDpi : settings.Dpi;

        var page = document.AddPage();
        page.Width = XUnit.FromInch(image.Width / dpiX);
        page.Height = XUnit.FromInch(image.Height / dpiY);

        // Formatos com poucas cores (linha/texto, o caso documental) usam PNG (sem perdas — JPEG
        // introduziria "ringing" ao redor de bordas de texto); Color usa JPEG, a escolha padrão
        // do mercado para digitalizações fotográficas coloridas, por um tamanho de arquivo menor.
        var isColor = image.Format is CorePixelFormat.Rgb24 or CorePixelFormat.Rgba32;
        var format = isColor ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png;
        var quality = isColor ? 85 : 100;

        var encoded = PdfSkiaConversions.Encode(image, format, quality);

        using var stream = new MemoryStream(encoded);
        using var xImage = XImage.FromStream(stream);
        using var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawImage(xImage, 0, 0, page.Width.Point, page.Height.Point);

        if (ocrResult is not null)
        {
            DrawInvisibleTextLayer(gfx, ocrResult, dpiX, dpiY);
        }
    }

    /// <summary>
    /// Desenha cada bloco reconhecido pelo OCR como texto totalmente transparente (alfa 0),
    /// posicionado exatamente sobre a região correspondente da imagem — invisível ao abrir o
    /// PDF, mas selecionável/pesquisável em qualquer leitor (seção 109-111). Não é o modo de
    /// renderização "invisível" (Tr 3) dedicado do PDF — o PDFsharp não expõe isso publicamente
    /// — mas o resultado prático (invisível + pesquisável) é o mesmo na grande maioria dos
    /// leitores.
    /// </summary>
    private static void DrawInvisibleTextLayer(XGraphics gfx, OcrResult ocrResult, double dpiX, double dpiY)
    {
        var brush = new XSolidBrush(XColor.FromArgb(0, 0, 0, 0));

        foreach (var block in ocrResult.Blocks)
        {
            if (string.IsNullOrWhiteSpace(block.Text))
            {
                continue;
            }

            var x = block.BoundingBox.X / dpiX * 72.0;
            var y = block.BoundingBox.Y / dpiY * 72.0;
            var width = block.BoundingBox.Width / dpiX * 72.0;
            var height = block.BoundingBox.Height / dpiY * 72.0;
            if (width <= 0 || height <= 0)
            {
                continue;
            }

            // Aproximação razoável de altura de caixa → tamanho de fonte (a proporção exata
            // varia por fonte, mas não importa aqui: o texto nunca é visto, só selecionado).
            var font = new XFont(EmbeddedFontResolver.FamilyName, Math.Max(1.0, height * 0.8));
            gfx.DrawString(block.Text, font, brush, new XRect(x, y, width, height), XStringFormats.TopLeft);
        }
    }

    private static void ValidatePageAgainstMode(RasterImage image, PdfMode mode)
    {
        switch (mode)
        {
            case PdfMode.Documental when image.Format != CorePixelFormat.Bilevel1:
                throw new PdfProcessingException(
                    "A imagem precisa estar em preto e branco (1 bit) para o PDF Documental.",
                    $"PdfMode.Documental exige {CorePixelFormat.Bilevel1}, imagem está em {image.Format}.");

            case PdfMode.Grayscale when image.Format != CorePixelFormat.Gray8:
                throw new PdfProcessingException(
                    "A imagem precisa estar em escala de cinza para este modo de PDF.",
                    $"PdfMode.Grayscale exige {CorePixelFormat.Gray8}, imagem está em {image.Format}.");

            case PdfMode.Color when image.Format is not (CorePixelFormat.Rgb24 or CorePixelFormat.Rgba32):
                throw new PdfProcessingException(
                    "A imagem precisa estar em cor para este modo de PDF.",
                    $"PdfMode.Color exige Rgb24/Rgba32, imagem está em {image.Format}.");
        }
    }
}
