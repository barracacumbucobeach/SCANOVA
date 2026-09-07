namespace SCANOVA.Core.Exceptions;

/// <summary>Falha ao descobrir, conectar-se a, ou digitalizar com um scanner.</summary>
public sealed class ScannerException : ScanovaException
{
    public ScannerException(string userMessage, string technicalMessage, Exception? inner = null)
        : base(userMessage, technicalMessage, inner)
    {
    }
}
