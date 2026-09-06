namespace SCANOVA.Core.Models;

/// <summary>
/// Resultado da detecção automática de documento em uma imagem (ver <c>IDocumentDetectionService</c>).
/// O objetivo é localizar a região do documento, nunca classificar juridicamente o seu conteúdo.
/// </summary>
public sealed class DetectionResult
{
    /// <summary>Indica se um quadrilátero de documento foi encontrado.</summary>
    public required bool DocumentFound { get; init; }

    /// <summary>Região detectada (quatro cantos), quando <see cref="DocumentFound"/> é verdadeiro.</summary>
    public CropRegion? Region { get; init; }

    /// <summary>Confiança da detecção, de 0.0 a 1.0. Só sugerir corte automático acima do limite configurado.</summary>
    public required double Confidence { get; init; }

    /// <summary>Ângulo de inclinação estimado, em graus (positivo = sentido horário).</summary>
    public double SkewAngleDegrees { get; init; }

    /// <summary>Estimativa de tamanho físico do documento (nunca uma classificação jurídica, ex.: "RG"/"CPF").</summary>
    public Enums.DocumentSize EstimatedSize { get; init; } = Enums.DocumentSize.Automatic;
}
