using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using PDFtoImage;
using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;

namespace SCANOVA.Pdf.PdfRasterizer;

/// <summary>
/// Implementação de <see cref="IPdfRasterizer"/> usando PDFtoImage (MIT, sobre o motor PDFium do
/// Chromium — Apache 2.0/BSD), que roda de forma idêntica em Windows/Linux/macOS. Cada página é
/// renderizada na resolução (DPI) solicitada, exatamente como um scanner rasterizaria uma folha
/// física — usado tanto para visualizar um PDF existente quanto como primeira etapa da conversão
/// PDF → TIFF/imagem (seção 31).
/// </summary>
/// <remarks>
/// Os atributos <see cref="SupportedOSPlatformAttribute"/> abaixo só espelham a mesma lista de
/// plataformas de desktop que o PDFtoImage declara (silencia CA1416) — o SCANOVA continua um
/// aplicativo Windows; nada aqui exige suporte real a Linux/macOS além do necessário para os
/// testes automatizados rodarem neste projeto multiplataforma.
/// </remarks>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class PdfToImagePdfRasterizer : IPdfRasterizer
{
    public Task<int> GetPageCountAsync(string pdfPath, CancellationToken cancellationToken = default)
    {
        EnsureFileExists(pdfPath);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() =>
        {
            try
            {
                using var stream = File.OpenRead(pdfPath);
                return Conversion.GetPageCount(stream);
            }
            catch (Exception ex)
            {
                throw new PdfProcessingException(
                    "Não foi possível ler o arquivo PDF.",
                    $"GetPageCount falhou para \"{pdfPath}\": {ex.Message}",
                    ex);
            }
        }, cancellationToken);
    }

    public Task<RasterImage> RasterizePageAsync(string pdfPath, int pageIndex, double dpi, CancellationToken cancellationToken = default)
    {
        EnsureFileExists(pdfPath);
        if (dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi), "O DPI de rasterização precisa ser maior que zero.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() =>
        {
            try
            {
                using var stream = File.OpenRead(pdfPath);
                var options = new RenderOptions(Dpi: (int)Math.Round(dpi));
                using var bitmap = Conversion.ToImage(stream, pageIndex, options: options);
                return PdfSkiaConversions.ToRasterImage(bitmap, dpi, dpi);
            }
            catch (ScanovaException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PdfProcessingException(
                    "Não foi possível converter a página do PDF em imagem.",
                    $"RasterizePageAsync falhou para \"{pdfPath}\" (página {pageIndex}): {ex.Message}",
                    ex);
            }
        }, cancellationToken);
    }

    public async IAsyncEnumerable<RasterImage> RasterizeAllAsync(string pdfPath, double dpi, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureFileExists(pdfPath);
        if (dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi), "O DPI de rasterização precisa ser maior que zero.");
        }

        var options = new RenderOptions(Dpi: (int)Math.Round(dpi));

        // Um iterador (yield return) não pode ficar dentro de um try/catch com catch — por isso
        // as exceções aqui (arquivo corrompido no meio da leitura, por exemplo) chegam ao
        // chamador como a exceção original do PDFtoImage/SkiaSharp, não embrulhadas em
        // PdfProcessingException como nos outros métodos desta classe.
        var stream = File.OpenRead(pdfPath);
        try
        {
            await foreach (var bitmap in Conversion.ToImagesAsync(stream, leaveOpen: true, options: options, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                using (bitmap)
                {
                    yield return PdfSkiaConversions.ToRasterImage(bitmap, dpi, dpi);
                }
            }
        }
        finally
        {
            stream.Dispose();
        }
    }

    private static void EnsureFileExists(string pdfPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfPath);

        if (!File.Exists(pdfPath))
        {
            throw new PdfProcessingException(
                "O arquivo PDF não foi encontrado.",
                $"Arquivo PDF inexistente: {pdfPath}");
        }
    }
}
