using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SCANOVA.App.Services;
using SCANOVA.App.Views;
using SCANOVA.Core.Enums;
using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;

namespace SCANOVA.App.ViewModels;

/// <summary>Item de origem de digitalização exibido no dropdown "Origem" (seção 9).</summary>
public sealed record ScanSourceOption(string Label, ScanSource Value);

/// <summary>
/// ViewModel da tela de digitalização (seção 9-12): descoberta de scanners, seleção de
/// origem/perfil, digitalização com progresso, e navegação para o visualizador ao concluir.
/// </summary>
public sealed partial class ScanViewModel : ObservableObject
{
    private readonly IScannerService _scannerService;
    private readonly INavigationService _navigation;
    private readonly INotificationService _notifications;

    public ObservableCollection<ScannerInfo> Scanners { get; } = new();

    public IReadOnlyList<ScanProfile> Profiles { get; } = ScanProfile.Default;

    public IReadOnlyList<ScanSourceOption> Sources { get; } = new[]
    {
        new ScanSourceOption("Automático", ScanSource.Automatic),
        new ScanSourceOption("Mesa (Flatbed)", ScanSource.Flatbed),
        new ScanSourceOption("Alimentador", ScanSource.Feeder),
        new ScanSourceOption("Alimentador — Frente e verso", ScanSource.FeederDuplex),
    };

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    private ScannerInfo? selectedScanner;

    [ObservableProperty]
    private ScanProfile selectedProfile;

    [ObservableProperty]
    private ScanSourceOption selectedSource;

    [ObservableProperty]
    private bool isLoadingScanners;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    private bool isScanning;

    [ObservableProperty]
    private double scanProgress;

    /// <summary>Verdadeiro quando a busca terminou e nenhum scanner foi encontrado (seção 9 — estado vazio).</summary>
    [ObservableProperty]
    private bool noScannersFound;

    public ScanViewModel(IScannerService scannerService, INavigationService navigation, INotificationService notifications)
    {
        _scannerService = scannerService;
        _navigation = navigation;
        _notifications = notifications;

        selectedProfile = Profiles[0]; // "TIFF Documental" — preset padrão do produto.
        selectedSource = Sources[0]; // "Automático"
    }

    public async Task InitializeAsync()
    {
        await RefreshScannersAsync();
    }

    [RelayCommand]
    private async Task RefreshScannersAsync()
    {
        IsLoadingScanners = true;
        NoScannersFound = false;
        try
        {
            var found = await _scannerService.DiscoverScannersAsync();

            Scanners.Clear();
            foreach (var scanner in found)
            {
                Scanners.Add(scanner);
            }

            SelectedScanner = Scanners.FirstOrDefault(s => s.IsDefault) ?? Scanners.FirstOrDefault();
            NoScannersFound = Scanners.Count == 0;
        }
        catch (ScanovaException ex)
        {
            NoScannersFound = true;
            _notifications.ShowError(ex.UserMessage);
        }
        catch (Exception)
        {
            NoScannersFound = true;
            _notifications.ShowError("Não foi possível buscar scanners instalados.");
        }
        finally
        {
            IsLoadingScanners = false;
        }
    }

    private bool CanScan() => SelectedScanner is not null && !IsScanning;

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        if (SelectedScanner is null)
        {
            return;
        }

        var settings = SelectedProfile.CreateSettings(SelectedScanner.Id) with
        {
            Source = SelectedSource.Value,
        };

        IsScanning = true;
        ScanProgress = 0;
        var progress = new Progress<double>(p => ScanProgress = p);

        try
        {
            var image = await _scannerService.ScanAsync(settings, progress);
            _navigation.NavigateTo(typeof(DocumentViewerPage), new DocumentViewerParameter(null, image));
        }
        catch (ScanovaException ex)
        {
            _notifications.ShowError(ex.UserMessage);
        }
        catch (Exception)
        {
            _notifications.ShowError("Não foi possível concluir a digitalização. Verifique o scanner e tente novamente.");
        }
        finally
        {
            IsScanning = false;
        }
    }
}
