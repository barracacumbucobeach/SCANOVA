namespace SCANOVA.Core.Interfaces;

/// <summary>
/// Logging técnico local (seção 59). Implementações NUNCA devem registrar conteúdo de
/// documentos, texto de OCR, CPF/RG/endereço ou imagens — apenas eventos técnicos.
/// </summary>
public interface ILogService
{
    void Info(string message, params object[] args);

    void Warning(string message, params object[] args);

    void Error(Exception? exception, string message, params object[] args);

    void Debug(string message, params object[] args);
}
