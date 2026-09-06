using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using SCANOVA.App.Services;
using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;

namespace SCANOVA.App.ViewModels;

/// <summary>
/// ViewModel do visualizador/editor básico de imagem (Fase 2): abrir, visualizar, girar, cortar
/// (manual) e exportar. Edição não destrutiva (seção 45) — <see cref="_original"/> nunca é
/// alterado; todas as operações trabalham sobre <see cref="_current"/> e o arquivo em disco só é
/// tocado quando o usuário pede explicitamente para salvar.
/// </summary>
public sealed partial class DocumentViewerViewModel : ObservableObject
{
    private readonly IImageService _imageService;
    private readonly IImageExporter _imageExporter;
    private readonly IFilePickerService _filePicker;
    private readonly INotificationService _notifications;

    private RasterImage? _original;
    private RasterImage? _current;
    private (double X, double Y, double Width, double Height)? _pendingCrop;

    [ObservableProperty]
    private WriteableBitmap? displayBitmap;

    [ObservableProperty]
    private string documentTitle = "Documento";

    [ObservableProperty]
    private bool isCropMode;

    [ObservableProperty]
    private bool isBusy;

    /// <summary>Verdadeiro quando existe uma seleção de corte pendente de confirmação.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCropCommand))]
    private bool hasPendingCrop;

    public DocumentViewerViewModel(
        IImageService imageService,
        IImageExporter imageExporter,
        IFilePickerService filePicker,
        INotificationService notifications)
    {
        _imageService = imageService;
        _imageExporter = imageExporter;
        _filePicker = filePicker;
        _notifications = notifications;
    }

    public async Task LoadAsync(string? sourcePath, RasterImage image)
    {
        _original = image;
        _current = image;
        DocumentTitle = sourcePath is null ? "Documento" : Path.GetFileName(sourcePath);
        await RefreshBitmapAsync();
    }

    private async Task RefreshBitmapAsync()
    {
        if (_current is null)
        {
            return;
        }

        DisplayBitmap = await RasterImageBitmapConverter.ToWriteableBitmapAsync(_current);
    }

    partial void OnIsCropModeChanged(bool value)
    {
        if (!value)
        {
            ClearPendingCrop();
        }
    }

    /// <summary>Chamado pelo code-behind da página ao soltar o arranjo de seleção de corte (coordenadas já em pixels da imagem).</summary>
    public void SetPendingCropSelection(double x, double y, double width, double height)
    {
        if (width < 4 || height < 4)
        {
            ClearPendingCrop();
            return;
        }

        _pendingCrop = (x, y, width, height);
        HasPendingCrop = true;
    }

    public void ClearPendingCrop()
    {
        _pendingCrop = null;
        HasPendingCrop = false;
    }

    [RelayCommand]
    private async Task RotateLeftAsync() => await RotateAsync(-90);

    [RelayCommand]
    private async Task RotateRightAsync() => await RotateAsync(90);

    private async Task RotateAsync(int degrees)
    {
        if (_current is null)
        {
            return;
        }

        var source = _current;
        // Seção 7/61: transformação de pixels não deve rodar na UI thread.
        _current = await Task.Run(() => _imageService.Rotate(source, degrees));
        ClearPendingCrop();
        await RefreshBitmapAsync();
    }

    private bool CanApplyCrop() => HasPendingCrop;

    [RelayCommand(CanExecute = nameof(CanApplyCrop))]
    private async Task ApplyCropAsync()
    {
        if (_current is null || _pendingCrop is not { } crop)
        {
            return;
        }

        var region = CropRegion.FromRectangle(crop.X, crop.Y, crop.Width, crop.Height);
        var source = _current;
        _current = await Task.Run(() => _imageService.Crop(source, region));
        ClearPendingCrop();
        IsCropMode = false;
        await RefreshBitmapAsync();
        _notifications.ShowSuccess("Corte aplicado.");
    }

    [RelayCommand]
    private void CancelCrop()
    {
        ClearPendingCrop();
        IsCropMode = false;
    }

    [RelayCommand]
    private async Task SaveAsAsync()
    {
        if (_current is null)
        {
            return;
        }

        var suggestedName = Path.GetFileNameWithoutExtension(DocumentTitle) is { Length: > 0 } n ? n : "Documento";
        var choices = new Dictionary<string, IList<string>>
        {
            ["Imagem PNG"] = new List<string> { ".png" },
            ["Imagem JPG"] = new List<string> { ".jpg" },
        };

        string? path;
        try
        {
            path = await _filePicker.PickSaveFileAsync(suggestedName, choices);
        }
        catch (Exception ex)
        {
            _notifications.ShowError("Não foi possível abrir a janela de salvar arquivo.");
            System.Diagnostics.Debug.WriteLine(ex);
            return;
        }

        if (path is null)
        {
            return; // usuário cancelou
        }

        IsBusy = true;
        try
        {
            await _imageExporter.SaveAsync(_current, path);
            _notifications.ShowSuccess("Documento salvo com sucesso.");
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
