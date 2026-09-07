using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SCANOVA.App.Services;
using SCANOVA.Core.Enums;
using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;

namespace SCANOVA.App.ViewModels;

/// <summary>Item de formato de saída exibido no dropdown de conversão em lote.</summary>
public sealed record BatchFormatOption(string Label, OutputFormat Value);

/// <summary>
/// ViewModel da conversão em lote (Fase 8): fila de arquivos, progresso, pausa/cancelamento e
/// relatório final — tudo delegado a <see cref="IBatchProcessingService"/>.
/// </summary>
public sealed partial class BatchConvertViewModel : ObservableObject
{
    private readonly IBatchProcessingService _batchService;
    private readonly IDocumentEnhancementService _enhancementService;
    private readonly IFilePickerService _filePicker;
    private readonly INotificationService _notifications;

    private readonly List<string> _sourcePaths = new();
    private CancellationTokenSource? _cts;
    private Guid _currentJobId;

    public ObservableCollection<string> SourceFileNames { get; } = new();

    public IReadOnlyList<BatchFormatOption> Formats { get; } = new[]
    {
        new BatchFormatOption("TIFF Documental (CCITT Group 4 — 200 DPI)", OutputFormat.TiffDocumental),
        new BatchFormatOption("TIFF", OutputFormat.Tiff),
        new BatchFormatOption("PDF", OutputFormat.Pdf),
        new BatchFormatOption("Imagem PNG", OutputFormat.Png),
        new BatchFormatOption("Imagem JPG", OutputFormat.Jpg),
    };

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private int fileCount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private string? destinationFolder;

    [ObservableProperty]
    private BatchFormatOption selectedFormat;

    [ObservableProperty]
    private bool enhanceAutomatically;

    [ObservableProperty]
    private bool overwriteExisting;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(PauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool isRunning;

    [ObservableProperty]
    private bool isPaused;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressStatusText))]
    private int processedCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressStatusText))]
    private int totalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressStatusText))]
    private string? currentFileName;

    public string ProgressStatusText => CurrentFileName is null
        ? $"{ProcessedCount} de {TotalCount}"
        : $"{ProcessedCount} de {TotalCount} — {CurrentFileName}";

    [ObservableProperty]
    private bool hasReport;

    [ObservableProperty]
    private string? reportSummary;

    [ObservableProperty]
    private bool hasFailedItems;

    public ObservableCollection<string> FailedItems { get; } = new();

    public BatchConvertViewModel(
        IBatchProcessingService batchService,
        IDocumentEnhancementService enhancementService,
        IFilePickerService filePicker,
        INotificationService notifications)
    {
        _batchService = batchService;
        _enhancementService = enhancementService;
        _filePicker = filePicker;
        _notifications = notifications;
        selectedFormat = Formats[0]; // TIFF Documental — preset padrão do produto.
    }

    [RelayCommand]
    private async Task AddFilesAsync()
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

        foreach (var path in paths)
        {
            _sourcePaths.Add(path);
            SourceFileNames.Add(Path.GetFileName(path));
        }

        FileCount = _sourcePaths.Count;
    }

    [RelayCommand]
    private void ClearFiles()
    {
        _sourcePaths.Clear();
        SourceFileNames.Clear();
        FileCount = 0;
    }

    [RelayCommand]
    private async Task PickDestinationFolderAsync()
    {
        try
        {
            DestinationFolder = await _filePicker.PickFolderAsync();
        }
        catch (Exception)
        {
            _notifications.ShowError("Não foi possível abrir a janela de seleção de pasta.");
        }
    }

    private bool CanStart() => !IsRunning && _sourcePaths.Count > 0 && !string.IsNullOrEmpty(DestinationFolder);

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        var job = new BatchJob
        {
            Id = Guid.NewGuid(),
            Items = _sourcePaths.Select(p => new BatchItem { Id = Guid.NewGuid(), SourcePath = p }).ToList(),
            ExportSettings = new ExportSettings
            {
                Format = SelectedFormat.Value,
                DestinationFolder = DestinationFolder!,
                PromptBeforeOverwrite = !OverwriteExisting,
            },
            // Preset "Normal" (seção 20) — o mesmo usado em "Melhorar automaticamente" no
            // visualizador; controles mais finos ficam para uma iteração futura desta tela.
            Adjustments = EnhanceAutomatically ? _enhancementService.ResolvePreset(EnhancementPreset.Normal) : ImageAdjustments.None,
        };

        _currentJobId = job.Id;
        _cts = new CancellationTokenSource();
        IsRunning = true;
        IsPaused = false;
        ProcessedCount = 0;
        TotalCount = job.TotalCount;
        CurrentFileName = null;
        HasReport = false;
        ReportSummary = null;
        HasFailedItems = false;
        FailedItems.Clear();

        var progress = new Progress<BatchProgress>(p =>
        {
            ProcessedCount = p.ProcessedCount;
            CurrentFileName = p.CurrentFileName is null ? null : Path.GetFileName(p.CurrentFileName);
        });

        try
        {
            var result = await _batchService.RunAsync(job, progress, _cts.Token);
            HasReport = true;
            ReportSummary = $"{result.SuccessCount} de {result.TotalCount} arquivo(s) convertido(s) com sucesso.";

            foreach (var failed in result.Items.Where(i => i.Status == ProcessingStatus.Failed))
            {
                FailedItems.Add($"{Path.GetFileName(failed.SourcePath)}: {failed.ErrorMessage}");
            }

            HasFailedItems = FailedItems.Count > 0;

            if (result.FailureCount == 0)
            {
                _notifications.ShowSuccess(ReportSummary);
            }
            else
            {
                _notifications.ShowError($"{result.FailureCount} arquivo(s) não puderam ser convertidos. Veja o relatório abaixo.");
            }
        }
        catch (OperationCanceledException)
        {
            HasReport = true;
            ReportSummary = $"Conversão cancelada — {job.SuccessCount} de {job.TotalCount} arquivo(s) já haviam sido convertidos.";
            _notifications.ShowError("Conversão em lote cancelada.");
        }
        catch (ScanovaException ex)
        {
            _notifications.ShowError(ex.UserMessage);
        }
        finally
        {
            IsRunning = false;
            IsPaused = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Pause()
    {
        _batchService.Pause(_currentJobId);
        IsPaused = true;
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Resume()
    {
        _batchService.Resume(_currentJobId);
        IsPaused = false;
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Cancel()
    {
        // Se estava pausado, retoma antes de cancelar — senão o laço fica esperando o portão de
        // pausa para sempre e nunca chega a checar o cancelamento.
        if (IsPaused)
        {
            _batchService.Resume(_currentJobId);
            IsPaused = false;
        }

        _cts?.Cancel();
    }
}
