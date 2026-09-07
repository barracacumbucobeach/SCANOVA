using SCANOVA.Core.Interfaces;
using SCANOVA.Infrastructure.FileSystem;
using Serilog;
using Serilog.Events;

namespace SCANOVA.Infrastructure.Logging;

/// <summary>
/// Implementação de <see cref="ILogService"/> baseada em Serilog, gravando um arquivo por dia
/// em <c>Logs/yyyy-MM-dd.log</c> (seção 59). Os chamadores são responsáveis por nunca passar
/// conteúdo de documento, texto de OCR ou dados pessoais nos parâmetros — esta classe apenas
/// grava o que recebe.
/// </summary>
public sealed class SerilogLogService : ILogService, IDisposable
{
    private readonly Serilog.Core.Logger _logger;

    public SerilogLogService()
    {
        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(AppPaths.LogsFolder, "scanova-.log"),
                rollingInterval: RollingInterval.Day,
                restrictedToMinimumLevel: LogEventLevel.Debug,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    public void Info(string message, params object[] args) => _logger.Information(message, args);

    public void Warning(string message, params object[] args) => _logger.Warning(message, args);

    public void Error(Exception? exception, string message, params object[] args) => _logger.Error(exception, message, args);

    public void Debug(string message, params object[] args) => _logger.Debug(message, args);

    public void Dispose() => _logger.Dispose();
}
