using SCANOVA.Core.Models;

namespace SCANOVA.Core.Interfaces;

/// <summary>Progresso de um <see cref="BatchJob"/> em execução.</summary>
public sealed record BatchProgress(int ProcessedCount, int TotalCount, string? CurrentFileName);

/// <summary>
/// Executa um <see cref="BatchJob"/> de conversão em lote: processa um item por vez, libera
/// recursos imediatamente, nunca apaga os originais, permite pausa/cancelamento e produz um
/// relatório final (seções 34-35, 63).
/// </summary>
public interface IBatchProcessingService
{
    Task<BatchJob> RunAsync(BatchJob job, IProgress<BatchProgress>? progress = null, CancellationToken cancellationToken = default);

    void Pause(Guid jobId);

    void Resume(Guid jobId);
}
