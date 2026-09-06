using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using SCANOVA.App.Services;
using SCANOVA.Core.Enums;
using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;

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
    private readonly ITiffDocumentPipeline _tiffPipeline;
    private readonly IPdfService _pdfService;
    private readonly IDocumentEnhancementService _enhancementService;
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
        ITiffDocumentPipeline tiffPipeline,
        IPdfService pdfService,
        IDocumentEnhancementService enhancementService,
        IFilePickerService filePicker,
        INotificationService notifications)
    {
        _imageService = imageService;
        _imageExporter = imageExporter;
        _tiffPipeline = tiffPipeline;
        _pdfService = pdfService;
        _enhancementService = enhancementService;
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

    /// <summary>
    /// "Melhorar automaticamente" (seção 20): detecta o documento, endireita, corrige
    /// perspectiva, remove fundo/ruído e ajusta tom — tudo em uma etapa, sem o usuário precisar
    /// entender os termos técnicos por trás (seção 131). Não é destrutivo: "Reverter" volta ao
    /// original a qualquer momento, antes de salvar.
    /// </summary>
    [RelayCommand]
    private async Task EnhanceAutomaticallyAsync()
    {
        if (_current is null)
        {
            return;
        }

        var source = _current;
        IsBusy = true;
        try
        {
            _current = await Task.Run(() => _enhancementService.AutoEnhance(source, EnhancementPreset.Normal));
            ClearPendingCrop();
            await RefreshBitmapAsync();
            _notifications.ShowSuccess("Documento melhorado automaticamente.");
        }
        catch (Exception)
        {
            _notifications.ShowError("Não foi possível melhorar o documento automaticamente.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RevertToOriginalAsync()
    {
        if (_original is null)
        {
            return;
        }

        _current = _original;
        ClearPendingCrop();
        await RefreshBitmapAsync();
        _notifications.ShowSuccess("Documento revertido para o original.");
    }

    // Seção 66: no menu de formato, mostrar "TIFF Documental (CCITT Group 4 — 200 DPI)" em vez
    // de apenas "TIFF" — ajuda o usuário a escolher o formato certo sem precisar saber o que
    // CCITT ou DPI significam.
    private const string TiffDocumentalLabel = "TIFF Documental (CCITT Group 4 — 200 DPI)";

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
            [TiffDocumentalLabel] = new List<string> { ".tif" },
            ["Documento PDF"] = new List<string> { ".pdf" },
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
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension is ".tif" or ".tiff")
            {
                await SaveAsTiffDocumentalAsync(path);
            }
            else if (extension == ".pdf")
            {
                await SaveAsPdfAsync(path);
            }
            else
            {
                await _imageExporter.SaveAsync(_current, path);
                _notifications.ShowSuccess("Documento salvo com sucesso.");
            }
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

    /// <summary>
    /// Fluxo do marco funcional principal (seção 152): processa a imagem atual pelo pipeline
    /// TIFF Documental, salva e valida automaticamente — o usuário só vê o resultado final
    /// (seção 87/131), nunca os termos técnicos do processo.
    /// </summary>
    private async Task SaveAsTiffDocumentalAsync(string path)
    {
        if (_current is null)
        {
            return;
        }

        var result = await _tiffPipeline.SaveDocumentalTiffAsync(_current, path);

        if (!result.Success)
        {
            _notifications.ShowError(result.UserMessage ?? "Não foi possível gerar o TIFF Documental.");
            return;
        }

        // Seção 26/90: confirmação com o checklist de validação e a eficiência da compressão.
        var sizeInfo = result is { OutputSizeBytes: { } outSize, OriginalSizeBytes: { } origSize } && origSize > 0
            ? $" ({FormatBytes(origSize)} → {FormatBytes(outSize)})"
            : string.Empty;

        _notifications.ShowSuccess($"TIFF Documental salvo e validado: 200 DPI, 1 bit, CCITT Group 4{sizeInfo}.");
    }

    /// <summary>
    /// Salva o documento atual como um PDF de página única (Fase 6). O modo do PDF é escolhido a
    /// partir do formato de pixel atual da imagem (preto e branco/escala de cinza/cor) — o
    /// usuário não precisa escolher isso manualmente.
    /// </summary>
    private async Task SaveAsPdfAsync(string path)
    {
        if (_current is null)
        {
            return;
        }

        var mode = _current.Format switch
        {
            CorePixelFormat.Bilevel1 => PdfMode.Documental,
            CorePixelFormat.Gray8 => PdfMode.Grayscale,
            _ => PdfMode.Color,
        };
        var dpi = _current.HorizontalDpi > 0 ? _current.HorizontalDpi : 200;
        var settings = new PdfSettings { Mode = mode, Dpi = dpi };

        await _pdfService.WritePdfAsync(new[] { _current }, settings, path);

        _notifications.ShowSuccess("Documento PDF salvo com sucesso.");
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024 * 1024)
        {
            return $"{bytes / (1024.0 * 1024.0):0.#} MB";
        }

        if (bytes >= 1024)
        {
            return $"{bytes / 1024.0:0.#} KB";
        }

        return $"{bytes} bytes";
    }
}
