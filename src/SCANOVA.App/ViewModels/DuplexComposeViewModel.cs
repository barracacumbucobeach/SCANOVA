using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SCANOVA.App.Services;
using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;

namespace SCANOVA.App.ViewModels;

/// <summary>
/// ViewModel da composição frente/verso (Fase 7): junta duas pilhas de páginas escaneadas ou
/// abertas separadamente (frente e verso) em um único documento multipágina, na ordem correta.
/// Para scanners sem alimentador com duplex automático — o fluxo natural aqui é escanear/abrir
/// todas as frentes com "Abrir documento" ou a tela de digitalização, virar a pilha física e
/// repetir para os versos, depois carregar os dois lotes aqui.
/// </summary>
public sealed partial class DuplexComposeViewModel : ObservableObject
{
    private readonly IDuplexCompositionService _compositionService;
    private readonly IImageLoader _imageLoader;
    private readonly ITiffDocumentPipeline _tiffPipeline;
    private readonly IFilePickerService _filePicker;
    private readonly INotificationService _notifications;

    private readonly List<RasterImage> _frontPages = new();
    private readonly List<RasterImage> _backPages = new();

    public ObservableCollection<string> FrontFileNames { get; } = new();
    public ObservableCollection<string> BackFileNames { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ComposeAndSaveCommand))]
    private int frontCount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ComposeAndSaveCommand))]
    private int backCount;

    [ObservableProperty]
    private bool reverseBackOrder;

    [ObservableProperty]
    private bool rotateBackPages180;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ComposeAndSaveCommand))]
    private bool isBusy;

    public DuplexComposeViewModel(
        IDuplexCompositionService compositionService,
        IImageLoader imageLoader,
        ITiffDocumentPipeline tiffPipeline,
        IFilePickerService filePicker,
        INotificationService notifications)
    {
        _compositionService = compositionService;
        _imageLoader = imageLoader;
        _tiffPipeline = tiffPipeline;
        _filePicker = filePicker;
        _notifications = notifications;
    }

    [RelayCommand]
    private async Task AddFrontPagesAsync() => await AddPagesAsync(_frontPages, FrontFileNames, updateCount: () => FrontCount = _frontPages.Count);

    [RelayCommand]
    private async Task AddBackPagesAsync() => await AddPagesAsync(_backPages, BackFileNames, updateCount: () => BackCount = _backPages.Count);

    [RelayCommand]
    private void ClearFrontPages()
    {
        _frontPages.Clear();
        FrontFileNames.Clear();
        FrontCount = 0;
    }

    [RelayCommand]
    private void ClearBackPages()
    {
        _backPages.Clear();
        BackFileNames.Clear();
        BackCount = 0;
    }

    private async Task AddPagesAsync(List<RasterImage> destination, ObservableCollection<string> fileNames, Action updateCount)
    {
        IReadOnlyList<string> paths;
        try
        {
            paths = await _filePicker.PickMultipleImageFilesAsync();
        }
        catch (Exception)
        {
            _notifications.ShowError("Não foi possível abrir a janela de seleção de arquivos.");
            return;
        }

        if (paths.Count == 0)
        {
            return; // usuário cancelou
        }

        IsBusy = true;
        try
        {
            foreach (var path in paths)
            {
                var image = await _imageLoader.LoadAsync(path);
                destination.Add(image);
                fileNames.Add(Path.GetFileName(path));
            }

            updateCount();
        }
        catch (ScanovaException ex)
        {
            _notifications.ShowError(ex.UserMessage);
        }
        catch (Exception)
        {
            _notifications.ShowError("Não foi possível abrir um dos arquivos selecionados.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanComposeAndSave() => !IsBusy && _frontPages.Count > 0 && _frontPages.Count == _backPages.Count;

    [RelayCommand(CanExecute = nameof(CanComposeAndSave))]
    private async Task ComposeAndSaveAsync()
    {
        IReadOnlyList<RasterImage> composed;
        try
        {
            var options = new DuplexCompositionOptions
            {
                ReverseBackOrder = ReverseBackOrder,
                RotateBackPages180 = RotateBackPages180,
            };
            composed = _compositionService.Compose(_frontPages, _backPages, options);
        }
        catch (ScanovaException ex)
        {
            _notifications.ShowError(ex.UserMessage);
            return;
        }

        var choices = new Dictionary<string, IList<string>>
        {
            ["TIFF Documental (CCITT Group 4 — 200 DPI)"] = new List<string> { ".tif" },
        };

        string? path;
        try
        {
            path = await _filePicker.PickSaveFileAsync("Documento_FrenteVerso", choices);
        }
        catch (Exception)
        {
            _notifications.ShowError("Não foi possível abrir a janela de salvar arquivo.");
            return;
        }

        if (path is null)
        {
            return; // usuário cancelou
        }

        IsBusy = true;
        try
        {
            var result = await _tiffPipeline.SaveDocumentalTiffMultiPageAsync(composed, path);

            if (!result.Success)
            {
                _notifications.ShowError(result.UserMessage ?? "Não foi possível gerar o documento.");
                return;
            }

            _notifications.ShowSuccess($"Documento frente e verso salvo: {composed.Count} páginas ({_frontPages.Count} folhas).");
        }
        catch (ScanovaException ex)
        {
            _notifications.ShowError(ex.UserMessage);
        }
        catch (Exception)
        {
            _notifications.ShowError("Não foi possível salvar o documento. Verifique se a pasta está disponível e tente novamente.");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
