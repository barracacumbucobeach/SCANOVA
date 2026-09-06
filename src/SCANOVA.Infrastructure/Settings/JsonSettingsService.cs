using System.Text.Json;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SCANOVA.Infrastructure.FileSystem;

namespace SCANOVA.Infrastructure.Settings;

/// <summary>Persiste <see cref="AppSettings"/> como JSON em disco (seção 48).</summary>
public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _filePath;
    private readonly ILogService? _log;

    public AppSettings Current { get; private set; } = new();

    public event EventHandler<AppSettings>? SettingsChanged;

    public JsonSettingsService(ILogService? log = null, string? filePath = null)
    {
        _log = log;
        _filePath = filePath ?? AppPaths.SettingsFilePath;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            Current = new AppSettings();
            return;
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var loaded = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
            Current = loaded ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Configurações corrompidas/inacessíveis não devem impedir o aplicativo de abrir — usa padrões.
            _log?.Warning("Não foi possível carregar as configurações de {Path}; usando padrões.", _filePath);
            _log?.Debug("Detalhe técnico: {Message}", ex.Message);
            Current = new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, _filePath, overwrite: true);

        Current = settings;
        SettingsChanged?.Invoke(this, settings);
    }
}
