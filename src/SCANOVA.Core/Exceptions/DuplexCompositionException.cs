namespace SCANOVA.Core.Exceptions;

/// <summary>Falha ao compor páginas de frente e verso escaneadas/abertas separadamente em um único documento.</summary>
public sealed class DuplexCompositionException : ScanovaException
{
    public DuplexCompositionException(string userMessage, string technicalMessage, Exception? inner = null)
        : base(userMessage, technicalMessage, inner)
    {
    }
}
