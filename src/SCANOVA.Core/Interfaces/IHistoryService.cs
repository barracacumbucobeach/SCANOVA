using SCANOVA.Core.Models;

namespace SCANOVA.Core.Interfaces;

/// <summary>Histórico local de documentos processados (seção 46).</summary>
public interface IHistoryService
{
    Task<IReadOnlyList<HistoryEntry>> GetRecentAsync(int maxCount = 100, CancellationToken cancellationToken = default);

    Task AddAsync(HistoryEntry entry, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid entryId, CancellationToken cancellationToken = default);
}
