namespace SCANOVA.Infrastructure.FileSystem;

/// <summary>
/// Centraliza a resolução de pastas usadas pelo aplicativo (configurações, logs, temporários,
/// histórico). Nunca usa o diretório de instalação do aplicativo para dados do usuário (seção 47).
/// </summary>
public static class AppPaths
{
    private const string AppFolderName = "SCANOVA";

    /// <summary>Pasta raiz de dados do usuário para o SCANOVA (%LOCALAPPDATA%\SCANOVA no Windows).</summary>
    public static string RootFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppFolderName);

    public static string LogsFolder => EnsureExists(Path.Combine(RootFolder, "Logs"));

    public static string TempFolder => EnsureExists(Path.Combine(RootFolder, "Temp"));

    public static string SettingsFilePath => Path.Combine(EnsureExists(Path.Combine(RootFolder, "Settings")), "settings.json");

    public static string HistoryFilePath => Path.Combine(EnsureExists(Path.Combine(RootFolder, "History")), "history.json");

    public static string LicenseFilePath => Path.Combine(EnsureExists(Path.Combine(RootFolder, "License")), "license.dat");

    private static string EnsureExists(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
