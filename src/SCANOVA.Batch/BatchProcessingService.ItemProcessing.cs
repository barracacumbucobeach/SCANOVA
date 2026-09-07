using SCANOVA.Core.Enums;
using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Models;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;

namespace SCANOVA.Batch;

public sealed partial class BatchProcessingService
{
    /// <summary>
    /// Processa um único item: carrega, aplica os ajustes do lote, codifica no formato pedido.
    /// Nunca deixa uma exceção de item derrubar o lote inteiro — falhas viram
    /// <see cref="BatchItem.Status"/> = <see cref="ProcessingStatus.Failed"/> com uma mensagem
    /// amigável em <see cref="BatchItem.ErrorMessage"/>, e o lote continua para o próximo item
    /// (seção 63 — o relatório final precisa refletir cada item individualmente). Um
    /// cancelamento é a única exceção que propaga, para que o laço em <see cref="RunAsync"/>
    /// pare o lote inteiro.
    /// </summary>
    private async Task ProcessItemAsync(BatchItem item, ExportSettings exportSettings, ImageAdjustments adjustments, CancellationToken cancellationToken)
    {
        item.Status = ProcessingStatus.InProgress;

        try
        {
            var outputPath = ResolveOutputPath(item.SourcePath, exportSettings);

            if (exportSettings.PromptBeforeOverwrite && File.Exists(outputPath))
            {
                // A pergunta interativa ao usuário é responsabilidade da UI, antes de iniciar o
                // lote (perguntar no meio de uma fila em segundo plano não faz sentido) — aqui,
                // isso vira uma falha clara para este item em vez de sobrescrever silenciosamente.
                throw new FileAccessException(
                    $"O arquivo \"{Path.GetFileName(outputPath)}\" já existe no destino.",
                    $"Arquivo de saída já existe e ExportSettings.PromptBeforeOverwrite=true: {outputPath}");
            }

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var source = await _imageLoader.LoadAsync(item.SourcePath, cancellationToken).ConfigureAwait(false);

            // Seção 7/61: processamento de pixels não deve rodar diretamente no chamador quando
            // vier de uma thread sensível — aqui já estamos fora da UI thread (RunAsync roda em
            // segundo plano), mas Task.Run mantém o padrão usado no resto do app.
            var processed = await Task.Run(() => _enhancementService.Apply(source, adjustments), cancellationToken).ConfigureAwait(false);

            await WriteOutputAsync(processed, outputPath, exportSettings, cancellationToken).ConfigureAwait(false);

            item.OutputPath = outputPath;
            item.Status = ProcessingStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            item.Status = ProcessingStatus.Cancelled;
            throw;
        }
        catch (ScanovaException ex)
        {
            item.Status = ProcessingStatus.Failed;
            item.ErrorMessage = ex.UserMessage;
        }
        catch (Exception)
        {
            item.Status = ProcessingStatus.Failed;
            item.ErrorMessage = "Não foi possível processar este arquivo. Verifique se ele não está corrompido.";
        }
    }

    private async Task WriteOutputAsync(RasterImage image, string outputPath, ExportSettings exportSettings, CancellationToken cancellationToken)
    {
        switch (exportSettings.Format)
        {
            case OutputFormat.TiffDocumental or OutputFormat.TiffMultiPage:
                {
                    // "Multipágina" não se aplica item a item em um lote (cada item vira um
                    // arquivo — seção 35); tratado como o preset Documental de página única.
                    var result = await _tiffPipeline.SaveDocumentalTiffAsync(image, outputPath, adjustments: null, cancellationToken).ConfigureAwait(false);
                    if (!result.Success)
                    {
                        throw new TiffEncodingException(
                            result.UserMessage ?? "Não foi possível gerar o TIFF Documental.",
                            result.TechnicalDetail ?? "SaveDocumentalTiffAsync falhou sem detalhe técnico.");
                    }

                    break;
                }

            case OutputFormat.Tiff:
                await _tiffEncoder.EncodeAsync(image, GenericTiffSettings(image), outputPath, cancellationToken).ConfigureAwait(false);
                break;

            case OutputFormat.Png or OutputFormat.Jpg:
                await _imageExporter.SaveAsync(image, outputPath, cancellationToken: cancellationToken).ConfigureAwait(false);
                break;

            case OutputFormat.Pdf:
                var pdfSettings = exportSettings.Pdf ?? InferPdfSettings(image);
                await _pdfService.WritePdfAsync(new[] { image }, pdfSettings, outputPath, ocrResults: null, cancellationToken).ConfigureAwait(false);
                break;

            case OutputFormat.PdfSearchable:
                {
                    // Seção 109-111: reconhece o texto do item (localmente — nenhuma imagem sai
                    // da máquina) e gera um PDF pesquisável com a camada de texto invisível na
                    // posição de cada palavra. Uma falha aqui vira falha do item, como qualquer
                    // outro passo do pipeline — nunca derruba o lote inteiro.
                    var ocrSettings = exportSettings.Ocr ?? OcrSettings.Default;
                    var ocrResult = await _ocrService.RecognizeAsync(image, ocrSettings, cancellationToken).ConfigureAwait(false);

                    var searchableSettings = new PdfSettings
                    {
                        Mode = PdfMode.Searchable,
                        Dpi = (exportSettings.Pdf ?? InferPdfSettings(image)).Dpi,
                        IncludeOcrTextLayer = true,
                    };
                    await _pdfService.WritePdfAsync(new[] { image }, searchableSettings, outputPath, new[] { ocrResult }, cancellationToken).ConfigureAwait(false);
                    break;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(exportSettings), exportSettings.Format, "Formato de exportação não suportado pela conversão em lote.");
        }
    }

    private static string ResolveOutputPath(string sourcePath, ExportSettings exportSettings)
    {
        // Cada item preserva o nome do arquivo de origem (só troca a extensão) — o padrão de
        // nome único (ExportSettings.FileNamePattern/ResolveFileName) é para o fluxo de "Salvar
        // como" de um único documento; num lote de N arquivos, nomear todos a partir do mesmo
        // timestamp colidiria.
        var stem = Path.GetFileNameWithoutExtension(sourcePath);
        var extension = ExtensionFor(exportSettings.Format);

        var folder = exportSettings.DestinationFolder;
        if (exportSettings.CreateDateSubfolder)
        {
            folder = Path.Combine(folder, DateTime.Now.ToString("yyyy-MM-dd"));
        }

        return Path.Combine(folder, stem + extension);
    }

    private static string ExtensionFor(OutputFormat format) => format switch
    {
        OutputFormat.TiffDocumental or OutputFormat.TiffMultiPage or OutputFormat.Tiff => ".tif",
        OutputFormat.Png => ".png",
        OutputFormat.Jpg => ".jpg",
        OutputFormat.Pdf or OutputFormat.PdfSearchable => ".pdf",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Formato de exportação desconhecido."),
    };

    private static TiffSettings GenericTiffSettings(RasterImage image) => new()
    {
        HorizontalDpi = image.HorizontalDpi > 0 ? image.HorizontalDpi : 200,
        VerticalDpi = image.VerticalDpi > 0 ? image.VerticalDpi : 200,
        ColorMode = image.Format switch
        {
            CorePixelFormat.Bilevel1 => ColorMode.BlackAndWhite1Bit,
            CorePixelFormat.Gray8 => ColorMode.Grayscale,
            _ => ColorMode.Color,
        },
        // Sem perdas em ambos os casos — "genérico" (seção: sem as restrições do preset
        // documental) ainda não deveria degradar a qualidade da imagem por padrão.
        Compression = image.Format == CorePixelFormat.Bilevel1 ? CompressionType.CcittGroup4 : CompressionType.Lzw,
    };

    private static PdfSettings InferPdfSettings(RasterImage image) => new()
    {
        Mode = image.Format switch
        {
            CorePixelFormat.Bilevel1 => PdfMode.Documental,
            CorePixelFormat.Gray8 => PdfMode.Grayscale,
            _ => PdfMode.Color,
        },
        Dpi = image.HorizontalDpi > 0 ? image.HorizontalDpi : 200,
    };
}
