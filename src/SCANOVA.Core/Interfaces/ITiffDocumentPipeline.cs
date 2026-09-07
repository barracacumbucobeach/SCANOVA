using SCANOVA.Core.Models;

namespace SCANOVA.Core.Interfaces;

/// <summary>
/// Orquestra o pipeline completo do preset TIFF Documental (seção 84/85/86 — "modo automático"):
/// escala de cinza → binarização → normalização de DPI → CCITT Group 4 → validação. É o serviço
/// por trás do botão "Salvar TIFF Documental" — o usuário não precisa saber que existe
/// binarização, DPI ou CCITT por trás (seção 131).
/// </summary>
public interface ITiffDocumentPipeline
{
    /// <summary>
    /// Processa <paramref name="source"/> (qualquer formato de pixel) e salva um TIFF
    /// documental válido em <paramref name="filePath"/>, revalidando o resultado antes de
    /// retornar. Nunca altera <paramref name="source"/>.
    /// </summary>
    Task<ProcessingResult> SaveDocumentalTiffAsync(
        RasterImage source,
        string filePath,
        ImageAdjustments? adjustments = null,
        CancellationToken cancellationToken = default);

    /// <summary>Mesma lógica de <see cref="SaveDocumentalTiffAsync"/>, para múltiplas páginas em um único arquivo (seção 68/70).</summary>
    Task<ProcessingResult> SaveDocumentalTiffMultiPageAsync(
        IReadOnlyList<RasterImage> sources,
        string filePath,
        ImageAdjustments? adjustments = null,
        CancellationToken cancellationToken = default);
}
