namespace SCANOVA.Core.Models;

/// <summary>Resultado de uma operação de processamento/exportação.</summary>
public sealed class ProcessingResult
{
    public required bool Success { get; init; }
    public string? OutputPath { get; init; }

    /// <summary>Mensagem amigável para exibição ao usuário (nunca uma exceção técnica — ver seção 60).</summary>
    public string? UserMessage { get; init; }

    /// <summary>Detalhe técnico, destinado apenas ao log local, nunca à UI.</summary>
    public string? TechnicalDetail { get; init; }

    public long? OutputSizeBytes { get; init; }
    public long? OriginalSizeBytes { get; init; }

    public static ProcessingResult Ok(string outputPath, long? outputSizeBytes = null, long? originalSizeBytes = null) => new()
    {
        Success = true,
        OutputPath = outputPath,
        OutputSizeBytes = outputSizeBytes,
        OriginalSizeBytes = originalSizeBytes,
    };

    public static ProcessingResult Fail(string userMessage, string? technicalDetail = null) => new()
    {
        Success = false,
        UserMessage = userMessage,
        TechnicalDetail = technicalDetail,
    };
}
