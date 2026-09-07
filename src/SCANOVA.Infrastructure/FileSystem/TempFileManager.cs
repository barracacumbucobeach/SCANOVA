using SCANOVA.Core.Interfaces;

namespace SCANOVA.Infrastructure.FileSystem;

/// <summary>
/// Gerencia a pasta de arquivos temporários específica do aplicativo (seção 47). Cada execução
/// usa uma subpasta própria (identificada pelo Process Id + timestamp); em uma inicialização
/// anterior encerrada de forma inesperada, subpastas remanescentes são limpas na próxima abertura.
/// </summary>
public sealed class TempFileManager
{
    private readonly ILogService? _log;

    /// <summary>Subpasta de temporários exclusiva desta execução do aplicativo.</summary>
    public string SessionFolder { get; }

    public TempFileManager(ILogService? log = null)
    {
        _log = log;
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        SessionFolder = Path.Combine(AppPaths.TempFolder, $"session-{Environment.ProcessId}-{DateTime.UtcNow:yyyyMMddHHmmss}-{uniqueSuffix}");
        Directory.CreateDirectory(SessionFolder);
    }

    /// <summary>Gera um caminho de arquivo temporário exclusivo dentro da pasta desta sessão.</summary>
    public string CreateTempFilePath(string extension)
    {
        var ext = extension.StartsWith('.') ? extension : "." + extension;
        return Path.Combine(SessionFolder, $"{Guid.NewGuid():N}{ext}");
    }

    /// <summary>
    /// Remove subpastas de sessões anteriores (de execuções encerradas inesperadamente) antes de
    /// iniciar uma nova sessão. Deve ser chamado uma vez na inicialização do aplicativo.
    /// </summary>
    public void CleanupStaleSessions()
    {
        if (!Directory.Exists(AppPaths.TempFolder))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(AppPaths.TempFolder))
        {
            if (string.Equals(directory, SessionFolder, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best-effort: um arquivo ainda em uso não deve impedir a inicialização.
                _log?.Warning("Não foi possível remover a pasta temporária antiga {Folder}.", directory);
            }
        }
    }

    /// <summary>Remove a pasta temporária desta sessão. Deve ser chamado no encerramento normal do aplicativo.</summary>
    public void CleanupCurrentSession()
    {
        try
        {
            if (Directory.Exists(SessionFolder))
            {
                Directory.Delete(SessionFolder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log?.Warning("Não foi possível limpar a pasta temporária da sessão {Folder}.", SessionFolder);
        }
    }
}
