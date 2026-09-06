using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SCANOVA.App.Services;
using SCANOVA.Core.Interfaces;

namespace SCANOVA.App.ViewModels;

/// <summary>
/// ViewModel do Dashboard (seção 7): título "O que você deseja fazer?" + os seis cards de
/// ação, mais a introdução de primeiro uso (seção 133).
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly ISettingsService _settings;

    [ObservableProperty]
    private bool showWelcomeBanner;

    public DashboardViewModel(INavigationService navigation, ISettingsService settings)
    {
        _navigation = navigation;
        _settings = settings;
        ShowWelcomeBanner = !_settings.Current.FirstRunCompleted;
    }

    [RelayCommand]
    private async Task DismissWelcomeAsync()
    {
        ShowWelcomeBanner = false;
        await _settings.SaveAsync(_settings.Current with { FirstRunCompleted = true });
    }

    [RelayCommand]
    private async Task ScanDocumentAsync()
    {
        await DismissWelcomeAsync();
        _navigation.NavigateToPlaceholder("Digitalizar documento", "A digitalização via WIA será implementada na Fase 4 (Scanner).");
    }

    [RelayCommand]
    private void OpenDocument() =>
        _navigation.NavigateToPlaceholder("Abrir documento", "A abertura de imagens e PDF será habilitada na Fase 2 (Imagens) e Fase 6 (PDF).");

    [RelayCommand]
    private void Convert() =>
        _navigation.NavigateToPlaceholder("Converter", "A conversão entre formatos e o processamento em lote serão implementados na Fase 8.");

    [RelayCommand]
    private void ComposeDuplex() =>
        _navigation.NavigateToPlaceholder("Frente + verso", "A composição frente e verso será implementada na Fase 7.");

    [RelayCommand]
    private void EnhanceDocument() =>
        _navigation.NavigateToPlaceholder("Melhorar documento", "O motor de melhoria automática será implementado na Fase 5 (Automação).");

    [RelayCommand]
    private void ExtractText() =>
        _navigation.NavigateToPlaceholder("Extrair texto", "O reconhecimento de texto local (OCR) será implementado na Fase 9.");
}
