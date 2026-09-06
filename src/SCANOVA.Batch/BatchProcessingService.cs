using SCANOVA.Core.Enums;
using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;

namespace SCANOVA.Batch;

/// <summary>
/// Implementação de <see cref="IBatchProcessingService"/>: processa os itens de um
/// <see cref="BatchJob"/> um de cada vez (nunca carrega todos os arquivos em memória ao mesmo
/// tempo — seção 34/63), liberando cada imagem assim que o item termina. Nunca apaga/modifica os
/// arquivos de origem; cada item vira exatamente um arquivo de saída (seção 35).
/// </summary>
public sealed partial class BatchProcessingService : IBatchProcessingService
{
    private readonly IImageLoader _imageLoader;
    private readonly IImageExporter _imageExporter;
    private readonly IDocumentEnhancementService _enhancementService;
    private readonly ITiffEncoder _tiffEncoder;
    private readonly ITiffDocumentPipeline _tiffPipeline;
    private readonly IPdfService _pdfService;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, PauseGate> _gates = new();

    public BatchProcessingService(
        IImageLoader imageLoader,
        IImageExporter imageExporter,
        IDocumentEnhancementService enhancementService,
        ITiffEncoder tiffEncoder,
        ITiffDocumentPipeline tiffPipeline,
        IPdfService pdfService)
    {
        _imageLoader = imageLoader;
        _imageExporter = imageExporter;
        _enhancementService = enhancementService;
        _tiffEncoder = tiffEncoder;
        _tiffPipeline = tiffPipeline;
        _pdfService = pdfService;
    }

    public async Task<BatchJob> RunAsync(BatchJob job, IProgress<BatchProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (job.ExportSettings.Format == OutputFormat.PdfSearchable)
        {
            // Falha o lote inteiro de uma vez, antes de processar qualquer item — mais claro do
            // que descobrir isso item a item (seção 109: depende do OCR, ainda não implementado).
            throw new PdfProcessingException(
                "PDF pesquisável (com camada de texto do OCR) será implementado na Fase 9.",
                $"{nameof(OutputFormat.PdfSearchable)} ainda não é suportado por {nameof(BatchProcessingService)}.");
        }

        var gate = _gates.GetOrAdd(job.Id, static _ => new PauseGate());
        try
        {
            job.Status = ProcessingStatus.InProgress;

            foreach (var item in job.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await gate.WaitWhilePausedAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                await ProcessItemAsync(item, job.ExportSettings, job.Adjustments, cancellationToken).ConfigureAwait(false);

                progress?.Report(new BatchProgress(job.ProcessedCount, job.TotalCount, item.SourcePath));
            }

            job.Status = job.FailureCount > 0 ? ProcessingStatus.Failed : ProcessingStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            job.Status = ProcessingStatus.Cancelled;

            // Itens que ainda não começaram (ou que estavam em andamento quando o cancelamento
            // chegou) ficam marcados como cancelados — nunca "pendente" para sempre num relatório
            // final (seção 63: o relatório precisa refletir com precisão o que aconteceu).
            foreach (var pending in job.Items.Where(i => i.Status is ProcessingStatus.Pending or ProcessingStatus.InProgress))
            {
                pending.Status = ProcessingStatus.Cancelled;
            }

            throw;
        }
        finally
        {
            _gates.TryRemove(job.Id, out _);
        }

        return job;
    }

    public void Pause(Guid jobId) => _gates.GetOrAdd(jobId, static _ => new PauseGate()).Pause();

    public void Resume(Guid jobId) => _gates.GetOrAdd(jobId, static _ => new PauseGate()).Resume();
}
