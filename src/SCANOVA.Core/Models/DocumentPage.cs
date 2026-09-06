namespace SCANOVA.Core.Models;

/// <summary>Uma página de um <see cref="Document"/>.</summary>
public sealed class DocumentPage
{
    public required Guid Id { get; init; }
    public required int Index { get; set; }

    /// <summary>Imagem original desta página, tal como carregada/digitalizada — nunca sobrescrita em memória.</summary>
    public required RasterImage Original { get; init; }

    /// <summary>Resultado do pipeline de processamento aplicado até o momento (preview). Pode ser nulo se ainda não processada.</summary>
    public RasterImage? Processed { get; set; }

    public int RotationDegrees { get; set; }
    public CropRegion? Crop { get; set; }
    public ImageAdjustments Adjustments { get; set; } = ImageAdjustments.None;
}
