namespace SCANOVA.Core.Exceptions;

/// <summary>Falha de ativação/validação de licença.</summary>
public sealed class LicenseException : ScanovaException
{
    public LicenseException(string userMessage, string technicalMessage, Exception? inner = null)
        : base(userMessage, technicalMessage, inner)
    {
    }
}
