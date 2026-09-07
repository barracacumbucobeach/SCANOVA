namespace SCANOVA.Core.Exceptions;

/// <summary>Falha ao carregar/decodificar um arquivo de imagem (formato não suportado, corrompido, etc.).</summary>
public sealed class ImageLoadException : ScanovaException
{
    public ImageLoadException(string userMessage, string technicalMessage, Exception? inner = null)
        : base(userMessage, technicalMessage, inner)
    {
    }
}
