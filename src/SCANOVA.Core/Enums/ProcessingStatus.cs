namespace SCANOVA.Core.Enums;

/// <summary>Estado de uma operação de processamento (item de lote, exportação, digitalização etc.).</summary>
public enum ProcessingStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled,
}
