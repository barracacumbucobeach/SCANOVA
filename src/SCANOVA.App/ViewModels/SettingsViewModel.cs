using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SCANOVA.App.Services;
using SCANOVA.Core.Enums;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;

namespace SCANOVA.App.ViewModels;

/// <summary>
/// ViewModel da tela "Configurações" (seção 48) — nesta fase, principalmente a seção de
/// licenciamento (Fase 10): status da licença, ativação por chave, e desativação. Uma seção
/// "Sobre" simples também fica aqui, por ser um complemento natural e barato de adicionar junto.
/// As demais seções de configurações (aparência, acessibilidade etc.) ficam para a Fase 11.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ILicenseService _licenseService;
    private readonly INotificationService _notifications;

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

    public SettingsViewModel(ILicenseService licenseService, INotificationService notifications)
    {
        _licenseService = licenseService;
        _notifications = notifications;
        Refresh();
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
