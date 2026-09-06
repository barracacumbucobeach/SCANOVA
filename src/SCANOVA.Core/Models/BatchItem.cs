using SCANOVA.Core.Enums;

namespace SCANOVA.Core.Models;

/// <summary>Um item (arquivo) dentro de um <see cref="BatchJob"/> de conversão em lote.</summary>
public sealed class BatchItem
{
    public required Guid Id { get; init; }
    public required string SourcePath { get; init; }
    public string? OutputPath { get; set; }
    public ProcessingStatus Status { get; set; } = ProcessingStatus.Pending;

    /// <summary>Mensagem amigável de erro, preenchida quando <see cref="Status"/> é <see cref="ProcessingStatus.Failed"/>.</summary>
    public string? ErrorMessage { get; set; }
}
