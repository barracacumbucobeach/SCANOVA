namespace SCANOVA.Core.Exceptions;

/// <summary>Falha ao ler, rasterizar ou gerar um arquivo PDF.</summary>
public sealed class PdfProcessingException : ScanovaException
{
    public PdfProcessingException(string userMessage, string technicalMessage, Exception? inner = null)
        : base(userMessage, technicalMessage, inner)
    {
    }
}
