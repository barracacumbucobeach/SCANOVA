namespace SCANOVA.Core.Models;

/// <summary>Caixa delimitadora, em pixels, da imagem de entrada do OCR.</summary>
public readonly record struct BoundingBox(int X, int Y, int Width, int Height);

/// <summary>Um bloco de texto reconhecido (linha ou parágrafo, dependendo do motor).</summary>
public sealed class OcrBlock
{
    public required string Text { get; init; }
    public required BoundingBox BoundingBox { get; init; }

    /// <summary>Confiança de 0.0 a 1.0, quando fornecida de forma confiável pelo motor (seção 108). Nulo caso contrário.</summary>
    public double? Confidence { get; init; }
}

/// <summary>Resultado do reconhecimento de texto (OCR) de uma imagem ou página (seção 107).</summary>
public sealed class OcrResult
{
    public required string Text { get; init; }
    public double? Confidence { get; init; }
    public required IReadOnlyList<OcrBlock> Blocks { get; init; }
}
