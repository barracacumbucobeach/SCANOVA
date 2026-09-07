namespace SCANOVA.Core.Exceptions;

/// <summary>
/// Base de todas as exceções de domínio do SCANOVA. <see cref="UserMessage"/> contém um texto
/// amigável, seguro para exibição direta ao usuário final (ver seção 60 da especificação);
/// a mensagem técnica completa (<see cref="Exception.Message"/>/<see cref="Exception.InnerException"/>)
/// deve ir apenas para o log.
/// </summary>
public abstract class ScanovaException : Exception
{
    public string UserMessage { get; }

    protected ScanovaException(string userMessage, string technicalMessage, Exception? inner = null)
        : base(technicalMessage, inner)
    {
        UserMessage = userMessage;
    }
}
