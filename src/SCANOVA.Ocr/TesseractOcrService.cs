using System.Diagnostics;
using System.Globalization;
using SCANOVA.Core.Enums;
using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SCANOVA.Ocr.Tesseract;

namespace SCANOVA.Ocr;

/// <summary>
/// Implementação de <see cref="IOcrService"/> usando o executável do Tesseract (Apache 2.0,
/// via NAPS2.Tesseract.Binaries) como processo externo — nunca P/Invoke, o que evita qualquer
/// complexidade de interoperabilidade nativa. Roda inteiramente local (seção 40): a imagem
/// grava um arquivo temporário próprio, nunca o documento original, e é apagada ao final
/// (seção 43).
/// </summary>
public sealed class TesseractOcrService : IOcrService
{
    private readonly IImageExporter _imageExporter;
    private readonly TesseractLanguageDataProvider _languageData;
    private readonly Lazy<string> _tesseractPath;

    public TesseractOcrService(IImageExporter imageExporter, string languageDataCacheFolder)
    {
        _imageExporter = imageExporter;
        _languageData = new TesseractLanguageDataProvider(languageDataCacheFolder);
        // Adiado (Lazy) — permite construir o serviço mesmo se o executável não puder ser
        // localizado ainda, e só falhar quando o OCR for de fato usado.
        _tesseractPath = new Lazy<string>(TesseractExecutableLocator.Locate);
    }

    public Task<IReadOnlyList<OcrLanguage>> GetAvailableLanguagesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_languageData.GetLocallyAvailableLanguages());

    public async Task<OcrResult> RecognizeAsync(RasterImage image, OcrSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        var workDir = Path.Combine(Path.GetTempPath(), $"scanova-ocr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        try
        {
            var languageDataFolder = await _languageData.EnsureAvailableAsync(settings.Language, settings.CorrectOrientation, cancellationToken).ConfigureAwait(false);

            var imagePath = Path.Combine(workDir, "input.png");
            await _imageExporter.SaveAsync(image, imagePath, cancellationToken: cancellationToken).ConfigureAwait(false);

            var outputBasePath = Path.Combine(workDir, "output");
            await RunTesseractAsync(imagePath, outputBasePath, languageDataFolder, settings, cancellationToken).ConfigureAwait(false);

            return HocrParser.Parse(outputBasePath + ".hocr", settings.PreserveLineBreaks);
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
            throw new OcrException(
                "Não foi possível reconhecer o texto do documento.",
                $"Falha no OCR: {ex.Message}",
                ex);
        }
        finally
        {
            TryDeleteDirectory(workDir);
        }
    }

    public async Task<OcrResult> RecognizeMultiPageAsync(IReadOnlyList<RasterImage> pages, OcrSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(settings);

        if (pages.Count == 0)
        {
            throw new OcrException(
                "Não há páginas para reconhecer.",
                $"{nameof(RecognizeMultiPageAsync)} chamado com uma lista de páginas vazia.");
        }

        var textParts = new List<string>(pages.Count);
        var allBlocks = new List<OcrBlock>();
        var confidences = new List<double>();

        foreach (var page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await RecognizeAsync(page, settings, cancellationToken).ConfigureAwait(false);

            textParts.Add(result.Text);
            allBlocks.AddRange(result.Blocks);
            if (result.Confidence.HasValue)
            {
                confidences.Add(result.Confidence.Value);
            }
        }

        // Seção 111: concatena o texto de todas as páginas, na ordem correta, com uma separação
        // clara entre páginas (linha em branco) quando quebras de linha são preservadas.
        var separator = settings.PreserveLineBreaks ? "\n\n" : " ";

        return new OcrResult
        {
            Text = string.Join(separator, textParts),
            Confidence = confidences.Count > 0 ? confidences.Average() : null,
            Blocks = allBlocks,
        };
    }

    private async Task RunTesseractAsync(string imagePath, string outputBasePath, string languageDataFolder, OcrSettings settings, CancellationToken cancellationToken)
    {
        var languageCode = TesseractLanguageDataProvider.CodeFor(settings.Language);
        // --psm 1: segmentação automática de página COM detecção de orientação/script (exige
        // osd.traineddata); --psm 3: segmentação automática simples, sem detectar orientação.
        var pageSegmentationMode = settings.CorrectOrientation ? 1 : 3;

        var startInfo = new ProcessStartInfo
        {
            FileName = _tesseractPath.Value,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(imagePath);
        startInfo.ArgumentList.Add(outputBasePath);
        startInfo.ArgumentList.Add("-l");
        startInfo.ArgumentList.Add(languageCode);
        startInfo.ArgumentList.Add("--psm");
        startInfo.ArgumentList.Add(pageSegmentationMode.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("tessedit_create_hocr=1");
        startInfo.EnvironmentVariables["TESSDATA_PREFIX"] = languageDataFolder;

        using var process = Process.Start(startInfo)
            ?? throw new OcrException(
                "Não foi possível iniciar o processo de reconhecimento de texto.",
                "Process.Start retornou null para o executável do Tesseract.");

        // Garante que o processo não fique órfão se o cancelamento chegar antes dele terminar.
        await using var killOnCancel = cancellationToken.Register(() => TryKill(process));

        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new OcrException(
                "O reconhecimento de texto falhou.",
                $"tesseract saiu com código {process.ExitCode}: {stderr}");
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort — o processo pode já ter terminado entre a checagem e o Kill.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort — nunca falha o reconhecimento por causa de limpeza de temporários.
        }
    }
}
