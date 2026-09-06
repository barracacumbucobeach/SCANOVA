using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SCANOVA.App.Services;
using SCANOVA.Core.Enums;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;

namespace SCANOVA.App.ViewModels;

/// <summary>Item de aparência exibido no seletor de tema (Fase 11 — modo escuro).</summary>
public sealed record ThemeOption(string Label, string Value);

/// <summary>
/// ViewModel da tela "Configurações" (seção 48): aparência (Fase 11 — modo escuro/claro/padrão
/// do sistema), licenciamento (Fase 10 — status da licença, ativação por chave, desativação) e
/// uma seção "Sobre" simples.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ILicenseService _licenseService;
    private readonly ISettingsService _settings;
    private readonly IThemeService _themeService;
    private readonly INotificationService _notifications;

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
    [
        new("Padrão do sistema", "System"),
        new("Claro", "Light"),
        new("Escuro", "Dark"),
    ];

    [ObservableProperty]
    private ThemeOption selectedTheme;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(IsLicensed))]
    private LicenseStatus status;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLicenseInfo))]
    [NotifyPropertyChangedFor(nameof(EditionText))]
    [NotifyPropertyChangedFor(nameof(CustomerText))]
    [NotifyPropertyChangedFor(nameof(LicenseIdText))]
    private LicenseInfo? info;

    [ObservableProperty]
    private string licenseKeyInput = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ActivateCommand))]
    private bool isBusy;

    public bool IsLicensed => Status == LicenseStatus.Licensed;

    public bool HasLicenseInfo => Info is not null;

    public string EditionText => Info is null ? string.Empty : $"Edição: {Info.Edition}";

    public string CustomerText => Info?.CustomerName is { Length: > 0 } name ? $"Titular: {name}" : string.Empty;

    public string LicenseIdText => Info is null ? string.Empty : $"Identificador da licença: {Info.LicenseId}";

    public string StatusText => Status switch
    {
        LicenseStatus.Licensed => "Licença ativa. Obrigado por adquirir o SCANOVA!",
        LicenseStatus.Revoked => "Esta licença foi revogada. Entre em contato com o suporte.",
        LicenseStatus.Invalid => "A licença armazenada não pôde ser validada. Tente ativar novamente.",
        _ => "Nenhuma licença ativada.",
    };

    public string AppVersionText
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var number = version is null ? "—" : $"{version.Major}.{version.Minor}.{version.Build}";
            return $"SCANOVA — versão {number}";
        }
    }

    public SettingsViewModel(ILicenseService licenseService, ISettingsService settings, IThemeService themeService, INotificationService notifications)
    {
        _licenseService = licenseService;
        _settings = settings;
        _themeService = themeService;
        _notifications = notifications;
        selectedTheme = ThemeOptions.FirstOrDefault(o => o.Value == _settings.Current.Theme) ?? ThemeOptions[0];
        Refresh();
    }

    partial void OnSelectedThemeChanged(ThemeOption value)
    {
        _themeService.Apply(value.Value);
        _ = SaveThemePreferenceAsync(value.Value);
    }

    private async Task SaveThemePreferenceAsync(string theme)
    {
        try
        {
            await _settings.SaveAsync(_settings.Current with { Theme = theme });
        }
        catch (Exception)
        {
            _notifications.ShowError("Não foi possível salvar a preferência de aparência.");
        }
    }

    private void Refresh()
    {
        Status = _licenseService.GetLicenseStatus();
        Info = _licenseService.GetLicenseInfo();
    }

    private bool CanActivate() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanActivate))]
    private async Task ActivateAsync()
    {
        if (string.IsNullOrWhiteSpace(LicenseKeyInput))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _licenseService.ActivateAsync(LicenseKeyInput);
            if (result.Success)
            {
                LicenseKeyInput = string.Empty;
                Refresh();
                _notifications.ShowSuccess("Licença ativada com sucesso.");
            }
            else
            {
                _notifications.ShowError(result.UserMessage ?? "Não foi possível ativar esta chave de licença.");
            }
        }
        catch (Exception)
        {
            _notifications.ShowError("Não foi possível ativar a licença. Verifique a chave informada.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeactivateAsync()
    {
        IsBusy = true;
        try
        {
            await _licenseService.DeactivateAsync();
            Refresh();
            _notifications.ShowSuccess("Licença desativada nesta máquina.");
        }
        catch (Exception)
        {
            _notifications.ShowError("Não foi possível desativar a licença.");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
