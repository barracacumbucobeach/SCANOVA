using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using SCANOVA.App.Services;
using SCANOVA.Core.Enums;
using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;

namespace SCANOVA.App.ViewModels;

/// <summary>Item de idioma exibido no seletor de OCR.</summary>
public sealed record OcrLanguageOption(string Label, OcrLanguage Value);

/// <summary>
/// ViewModel do reconhecimento de texto (OCR, Fase 9): abrir um documento, reconhecer o texto
/// localmente (Tesseract, seção 40/107 — nunca enviado a nenhum serviço externo), revisar/editar
/// o resultado e salvar como TXT, DOCX ou PDF pesquisável (imagem original + camada de texto
/// invisível na posição reconhecida, seção 109-111).
/// </summary>
public sealed partial class OcrViewModel : ObservableObject
{
    private readonly IOcrService _ocrService;
    private readonly ITextDocumentExporter _textExporter;
    private readonly IPdfService _pdfService;
    private readonly IImageLoader _imageLoader;
    private readonly IFilePickerService _filePicker;
    private readonly INotificationService _notifications;

    private RasterImage? _image;
    private OcrResult? _lastResult;

    public IReadOnlyList<OcrLanguageOption> Languages { get; } = new[]
    {
        new OcrLanguageOption("Português (Brasil)", OcrLanguage.PortugueseBrazil),
        new OcrLanguageOption("Português", OcrLanguage.Portuguese),
        new OcrLanguageOption("Inglês", OcrLanguage.English),
        new OcrLanguageOption("Espanhol", OcrLanguage.Spanish),
        new OcrLanguageOption("Automático (Português + Inglês)", OcrLanguage.Automatic),
    };

    [ObservableProperty]
    private OcrLanguageOption selectedLanguage;

    [ObservableProperty]
    private WriteableBitmap? previewBitmap;

    [ObservableProperty]
    private string documentTitle = "Nenhum documento aberto";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RecognizeTextCommand))]
    private bool hasDocument;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RecognizeTextCommand))]
    private bool isBusy;

    [ObservableProperty]
    private string statusText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveAsTxtCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAsDocxCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAsSearchablePdfCommand))]
    private string recognizedText = string.Empty;

    public OcrViewModel(
        IOcrService ocrService,
        ITextDocumentExporter textExporter,
        IPdfService pdfService,
        IImageLoader imageLoader,
        IFilePickerService filePicker,
        INotificationService notifications)
    {
        _ocrService = ocrService;
        _textExporter = textExporter;
        _pdfService = pdfService;
        _imageLoader = imageLoader;
        _filePicker = filePicker;
        _notifications = notifications;
        selectedLanguage = Languages[0]; // Português (Brasil) — mercado principal do produto.
    }

    /// <summary>Chamado pela navegação quando o documento já foi escolhido em outra tela (ex.: cartão "Extrair texto" do Início).</summary>
    public async Task LoadAsync(string? sourcePath, RasterImage image)
    {
        _image = image;
        _lastResult = null;
        RecognizedText = string.Empty;
        DocumentTitle = sourcePath is null ? "Documento" : Path.GetFileName(sourcePath);
        HasDocument = true;
        StatusText = string.Empty;
        await RefreshPreviewAsync();
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

        IsBusy = true;
        try
        {
            var image = await _imageLoader.LoadAsync(path);
            await LoadAsync(path, image);
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
            IsBusy = false;
        }
    }

    private async Task RefreshPreviewAsync()
    {
        PreviewBitmap = _image is null ? null : await RasterImageBitmapConverter.ToWriteableBitmapAsync(_image);
    }

    private bool CanRecognizeText() => HasDocument && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRecognizeText))]
    private async Task RecognizeTextAsync()
    {
        if (_image is null)
        {
            return;
        }

        var image = _image;
        var settings = new OcrSettings { Language = SelectedLanguage.Value };

        IsBusy = true;
        StatusText = "Reconhecendo texto...";
        try
        {
            _lastResult = await _ocrService.RecognizeAsync(image, settings);
            RecognizedText = _lastResult.Text;

            StatusText = string.IsNullOrWhiteSpace(_lastResult.Text)
                ? "Nenhum texto foi encontrado neste documento."
                : FormatConfidence(_lastResult.Confidence);

            _notifications.ShowSuccess("Texto reconhecido com sucesso.");
        }
        catch (ScanovaException ex)
        {
            StatusText = string.Empty;
            _notifications.ShowError(ex.UserMessage);
        }
        catch (Exception)
        {
            StatusText = string.Empty;
            _notifications.ShowError("Não foi possível reconhecer o texto deste documento.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string FormatConfidence(double? confidence) =>
        confidence is { } value
            ? $"Texto reconhecido com {value * 100:0}% de confiança em média."
            : "Texto reconhecido.";

    private bool HasRecognizedText() => !string.IsNullOrWhiteSpace(RecognizedText);

    [RelayCommand(CanExecute = nameof(HasRecognizedText))]
    private async Task SaveAsTxtAsync()
    {
        var path = await PickSavePathAsync("texto.txt", "Arquivo de texto (.txt)", ".txt");
        if (path is null)
        {
            return;
        }

        try
        {
            await _textExporter.SaveAsTextAsync(RecognizedText, path);
            _notifications.ShowSuccess("Texto salvo com sucesso.");
        }
        catch (ScanovaException ex)
        {
            _notifications.ShowError(ex.UserMessage);
        }
    }

    [RelayCommand(CanExecute = nameof(HasRecognizedText))]
    private async Task SaveAsDocxAsync()
    {
        var path = await PickSavePathAsync("texto.docx", "Documento do Word (.docx)", ".docx");
        if (path is null)
        {
            return;
        }

        try
        {
            await _textExporter.SaveAsDocxAsync(RecognizedText, path);
            _notifications.ShowSuccess("Documento do Word salvo com sucesso.");
        }
        catch (ScanovaException ex)
        {
            _notifications.ShowError(ex.UserMessage);
        }
    }

    [RelayCommand(CanExecute = nameof(HasRecognizedText))]
    private async Task SaveAsSearchablePdfAsync()
    {
        if (_image is null || _lastResult is null)
        {
            return;
        }

        var path = await PickSavePathAsync("documento.pdf", "PDF pesquisável (.pdf)", ".pdf");
        if (path is null)
        {
            return;
        }

        // O texto exibido pode ter sido corrigido manualmente pelo usuário após o
        // reconhecimento — a camada invisível do PDF é montada a partir dos blocos (posições)
        // originais do OCR, que continuam válidos porque a imagem em si não muda; só o texto
        // "solto" (fora dos blocos) reflete a correção manual.
        var resultToEmbed = new OcrResult
        {
            Text = RecognizedText,
            Confidence = _lastResult.Confidence,
            Blocks = _lastResult.Blocks,
        };

        var dpi = _image.HorizontalDpi > 0 ? _image.HorizontalDpi : 200;
        var settings = new PdfSettings { Mode = PdfMode.Searchable, Dpi = dpi, IncludeOcrTextLayer = true };

        IsBusy = true;
        try
        {
            await _pdfService.WritePdfAsync(new[] { _image }, settings, path, new[] { resultToEmbed });
            _notifications.ShowSuccess("PDF pesquisável salvo com sucesso.");
        }
        catch (ScanovaException ex)
        {
            _notifications.ShowError(ex.UserMessage);
        }
        catch (Exception)
        {
            _notifications.ShowError("Não foi possível salvar o PDF pesquisável.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<string?> PickSavePathAsync(string suggestedName, string label, string extension)
    {
        var choices = new Dictionary<string, IList<string>> { [label] = new List<string> { extension } };
        try
        {
            return await _filePicker.PickSaveFileAsync(suggestedName, choices);
        }
        catch (Exception)
        {
            _notifications.ShowError("Não foi possível abrir a janela de salvar arquivo.");
            return null;
        }
    }
}
