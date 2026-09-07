using SCANOVA.Core.Models;

namespace SCANOVA.Core.Interfaces;

/// <summary>
/// Reconhecimento de texto (OCR), executado localmente por padrão — o documento não é enviado
/// a nenhuma API externa (seção 40). Trabalha sobre uma cópia/processamento temporário; nunca
/// altera o documento original (seção 43).
/// </summary>
public interface IOcrService
{
    /// <summary>Idiomas atualmente disponíveis, de acordo com os modelos instalados (seção 41).</summary>
    Task<IReadOnlyList<Enums.OcrLanguage>> GetAvailableLanguagesAsync(CancellationToken cancellationToken = default);

    Task<OcrResult> RecognizeAsync(RasterImage image, OcrSettings settings, CancellationToken cancellationToken = default);

    /// <summary>Reconhece múltiplas páginas e concatena o texto na ordem correta (seção 111).</summary>
    Task<OcrResult> RecognizeMultiPageAsync(IReadOnlyList<RasterImage> pages, OcrSettings settings, CancellationToken cancellationToken = default);
}
