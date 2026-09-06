namespace SCANOVA.Core.Models;

/// <summary>
/// Conjunto de ajustes de imagem aplicados pelo motor de melhoria (<c>IDocumentEnhancementService</c>).
/// Todos os valores têm um padrão neutro (nenhuma alteração).
/// </summary>
public sealed class ImageAdjustments
{
    /// <summary>-100 a 100. 0 = sem alteração.</summary>
    public int Brightness { get; init; }

    /// <summary>-100 a 100. 0 = sem alteração.</summary>
    public int Contrast { get; init; }

    /// <summary>Gamma. 1.0 = sem alteração.</summary>
    public double Gamma { get; init; } = 1.0;

    /// <summary>0 a 100. 0 = sem nitidez adicional.</summary>
    public int Sharpness { get; init; }

    /// <summary>-100 a 100. 0 = sem alteração. Aplicável apenas em modo colorido.</summary>
    public int Saturation { get; init; }

    public bool ConvertToGrayscale { get; init; }
    public bool RemoveBackground { get; init; }
    public bool ReduceNoise { get; init; }
    public bool CorrectSkew { get; init; }
    public bool CorrectPerspective { get; init; }

    public bool Binarize { get; init; }

    /// <summary>Método de binarização, quando <see cref="Binarize"/> é verdadeiro.</summary>
    public ThresholdMethod ThresholdMethod { get; init; } = ThresholdMethod.Automatic;

    /// <summary>Limiar manual (0-255), usado quando <see cref="ThresholdMethod"/> é <see cref="ThresholdMethod.Global"/>.</summary>
    public byte GlobalThreshold { get; init; } = 128;

    /// <summary>Ajustes neutros — nenhuma operação é aplicada.</summary>
    public static ImageAdjustments None { get; } = new();
}

/// <summary>Método de cálculo de limiar para binarização.</summary>
public enum ThresholdMethod
{
    /// <summary>O sistema escolhe o melhor método (hoje: Otsu global).</summary>
    Automatic,

    /// <summary>Limiar único fixo para toda a imagem (Otsu ou valor manual).</summary>
    Global,

    /// <summary>Limiar calculado localmente por região, melhor para iluminação irregular.</summary>
    Adaptive,
}
