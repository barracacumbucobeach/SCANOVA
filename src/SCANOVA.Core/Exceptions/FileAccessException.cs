namespace SCANOVA.Core.Exceptions;

/// <summary>Falha de acesso a arquivo/pasta: permissão negada, disco cheio, caminho inválido, arquivo inexistente, etc.</summary>
public sealed class FileAccessException : ScanovaException
{
    public FileAccessException(string userMessage, string technicalMessage, Exception? inner = null)
        : base(userMessage, technicalMessage, inner)
    {
    }
}
