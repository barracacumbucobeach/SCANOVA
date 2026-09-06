using SCANOVA.Core.Enums;
using SCANOVA.Core.Exceptions;

namespace SCANOVA.Ocr.Tesseract;

/// <summary>
/// Garante que os arquivos de modelo de idioma (.traineddata) do Tesseract estejam disponíveis
/// localmente, baixando-os sob demanda (seção 41) na primeira vez que um idioma é usado —
/// nenhum modelo é embutido no instalador (alguns têm dezenas de MB). Fonte: tessdata_fast
/// (Apache 2.0), os modelos LSTM "rápidos" oficiais do próprio projeto Tesseract, adequados para
/// documentos impressos/digitalizados (o caso de uso do SCANOVA — não manuscritos).
/// </summary>
internal sealed class TesseractLanguageDataProvider
{
    private const string BaseUrl = "https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main/";
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(2) };

    private readonly string _cacheFolder;

    public TesseractLanguageDataProvider(string cacheFolder)
    {
        _cacheFolder = cacheFolder;
        Directory.CreateDirectory(_cacheFolder);
    }

    /// <summary>Código(s) de idioma do Tesseract para um <see cref="OcrLanguage"/> — mais de um, separados por "+", quando o Tesseract deve combinar modelos.</summary>
    public static string CodeFor(OcrLanguage language) => language switch
    {
        OcrLanguage.PortugueseBrazil or OcrLanguage.Portuguese => "por",
        OcrLanguage.English => "eng",
        OcrLanguage.Spanish => "spa",
        // O Tesseract não faz identificação automática de idioma; combinar português (mercado
        // principal do produto) com inglês é um padrão razoável para "Automático".
        OcrLanguage.Automatic => "por+eng",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Idioma de OCR não suportado."),
    };

    public bool IsAvailableLocally(OcrLanguage language) =>
        CodeFor(language).Split('+').All(code => File.Exists(TrainedDataPath(code)));

    public IReadOnlyList<OcrLanguage> GetLocallyAvailableLanguages() =>
        Enum.GetValues<OcrLanguage>().Where(IsAvailableLocally).ToList();

    /// <summary>
    /// Baixa (se necessário) os modelos de idioma requeridos e, quando pedido, o modelo de
    /// orientação/script (osd) — devolve a pasta que contém os .traineddata, para uso como
    /// TESSDATA_PREFIX.
    /// </summary>
    public async Task<string> EnsureAvailableAsync(OcrLanguage language, bool includeOrientationData, CancellationToken cancellationToken)
    {
        var codes = CodeFor(language).Split('+').ToList();
        if (includeOrientationData)
        {
            codes.Add("osd");
        }

        foreach (var code in codes)
        {
            await EnsureFileAsync(code, cancellationToken).ConfigureAwait(false);
        }

        return _cacheFolder;
    }

    private async Task EnsureFileAsync(string code, CancellationToken cancellationToken)
    {
        var path = TrainedDataPath(code);
        if (File.Exists(path))
        {
            return;
        }

        var tempPath = path + ".download";
        try
        {
            using var response = await HttpClient.GetAsync($"{BaseUrl}{code}.traineddata", HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using (var fileStream = File.Create(tempPath))
            {
                await response.Content.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TryDelete(tempPath);
            throw new OcrException(
                $"Não foi possível baixar o modelo de idioma \"{code}\" para o reconhecimento de texto. Verifique sua conexão com a internet.",
                $"Falha ao baixar {BaseUrl}{code}.traineddata: {ex.Message}",
                ex);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort — não mascara a exceção original do download.
        }
    }

    private string TrainedDataPath(string code) => Path.Combine(_cacheFolder, $"{code}.traineddata");
}
