using SCANOVA.Core.Models;

namespace SCANOVA.Core.Interfaces;

/// <summary>
/// Localiza a região ocupada por um documento dentro de uma imagem digitalizada/fotografada
/// (bordas, contornos, quadrilátero, perspectiva). O objetivo é encontrar a região do documento,
/// nunca classificar juridicamente o seu conteúdo (seção 13).
/// </summary>
public interface IDocumentDetectionService
{
    Task<DetectionResult> DetectAsync(RasterImage image, CancellationToken cancellationToken = default);
}
