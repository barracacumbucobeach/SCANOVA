using SCANOVA.Core.Models;

namespace SCANOVA.Core.Interfaces;

/// <summary>Leitura/gravação das configurações persistidas do aplicativo (seção 48).</summary>
public interface ISettingsService
{
    AppSettings Current { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);

    event EventHandler<AppSettings>? SettingsChanged;
}
