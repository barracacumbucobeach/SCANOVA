using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SCANOVA.App.Services;
using SCANOVA.App.Views;
using SCANOVA.Core.Exceptions;
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
    private readonly IFilePickerService _filePicker;
    private readonly IImageLoader _imageLoader;
    private readonly INotificationService _notifications;

    [ObservableProperty]
    private bool showWelcomeBanner;

    [ObservableProperty]
    private bool isOpeningDocument;

    public DashboardViewModel(
        INavigationService navigation,
        ISettingsService settings,
        IFilePickerService filePicker,
        IImageLoader imageLoader,
        INotificationService notifications)
    {
        _navigation = navigation;
        _settings = settings;
        _filePicker = filePicker;
        _imageLoader = imageLoader;
        _notifications = notifications;
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
        _navigation.NavigateTo(typeof(ScanPage));
    }

    [RelayCommand]
    private async Task OpenDocumentAsync()
    {
        string? path;
        try
        {
            path = await _filePicker.PickImageFileAsync();
        }
        catch (Exception)
        {
            _notifications.ShowError("Não foi possível abrir a janela de seleção de arquivo.");
            return;
        }

        if (path is null)
        {
            return; // usuário cancelou
        }

        IsOpeningDocument = true;
        try
        {
            var image = await _imageLoader.LoadAsync(path);
            await DismissWelcomeAsync();
            _navigation.NavigateTo(typeof(DocumentViewerPage), new DocumentViewerParameter(path, image));
        }
        catch (ScanovaException ex)
        {
            _notifications.ShowError(ex.UserMessage);
        }
        catch (Exception)
        {
            _notifications.ShowError("Não foi possível abrir o arquivo selecionado.");
        }
        finally
        {
            IsOpeningDocument = false;
        }
    }

    [RelayCommand]
    private void Convert() =>
        _navigation.NavigateToPlaceholder("Converter", "A conversão entre formatos e o processamento em lote serão implementados na Fase 8.");

    [RelayCommand]
    private void ComposeDuplex() =>
        _navigation.NavigateToPlaceholder("Frente + verso", "A composição frente e verso será implementada na Fase 7.");

    /// <summary>
    /// "Melhorar documento" (seção 20): abre um documento existente diretamente no visualizador,
    /// onde "Melhorar automaticamente" (Fase 5) fica disponível — mesmo fluxo de abertura de
    /// <see cref="OpenDocumentAsync"/>, já que melhorar é uma ação sobre um documento aberto, não
    /// uma tela separada.
    /// </summary>
    [RelayCommand]
    private async Task EnhanceDocumentAsync() => await OpenDocumentAsync();

    [RelayCommand]
    private void ExtractText() =>
        _navigation.NavigateToPlaceholder("Extrair texto", "O reconhecimento de texto local (OCR) será implementado na Fase 9.");
}
