namespace SCANOVA.Core.Exceptions;

/// <summary>Falha ao reconhecer texto (OCR).</summary>
public sealed class OcrException : ScanovaException
{
    public OcrException(string userMessage, string technicalMessage, Exception? inner = null)
        : base(userMessage, technicalMessage, inner)
    {
    }
}
