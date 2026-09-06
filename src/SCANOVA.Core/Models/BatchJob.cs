using SCANOVA.Core.Enums;

namespace SCANOVA.Core.Models;

/// <summary>Um trabalho de conversão em lote: N arquivos de entrada, uma configuração de exportação comum.</summary>
public sealed class BatchJob
{
    public required Guid Id { get; init; }
    public required List<BatchItem> Items { get; init; }
    public required ExportSettings ExportSettings { get; init; }

    /// <summary>Ajustes aplicados a cada item (ex.: melhorar legibilidade, corrigir inclinação, corte automático).</summary>
    public ImageAdjustments Adjustments { get; init; } = ImageAdjustments.None;

    public ProcessingStatus Status { get; set; } = ProcessingStatus.Pending;
    public int ProcessedCount => Items.Count(i => i.Status is ProcessingStatus.Completed or ProcessingStatus.Failed or ProcessingStatus.Cancelled);
    public int SuccessCount => Items.Count(i => i.Status == ProcessingStatus.Completed);
    public int FailureCount => Items.Count(i => i.Status == ProcessingStatus.Failed);
    public int TotalCount => Items.Count;
}
