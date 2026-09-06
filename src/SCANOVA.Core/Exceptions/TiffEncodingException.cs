namespace SCANOVA.Core.Exceptions;

/// <summary>Falha ao codificar ou validar um arquivo TIFF.</summary>
public sealed class TiffEncodingException : ScanovaException
{
    public TiffEncodingException(string userMessage, string technicalMessage, Exception? inner = null)
        : base(userMessage, technicalMessage, inner)
    {
    }
}
