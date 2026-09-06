using SCANOVA.Core.Enums;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SCANOVA.Imaging.Composition;
using SCANOVA.Imaging.Detection;
using SCANOVA.Imaging.Enhancement;
using SCANOVA.Imaging.ImageLoading;
using SCANOVA.Imaging.ImageProcessing;
using SCANOVA.Ocr;
using SCANOVA.Pdf.PdfRasterizer;
using SCANOVA.Pdf.PdfWriter;
using SCANOVA.Tiff.TiffEncoder;
using SCANOVA.Tiff.TiffValidator;
using SkiaSharp;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;
using Xunit;

namespace SCANOVA.Batch.Tests;

#pragma warning disable CA1416 // PdfToImagePdfRasterizer declara SupportedOSPlatform windows/linux/macos — ver comentário equivalente em SCANOVA.Pdf.Tests.
public class BatchProcessingServiceTests : IDisposable
{
    private readonly string _sourceDir;
    private readonly string _destDir;
    private readonly IImageLoader _imageLoader = new SkiaImageLoader();
    private readonly IImageExporter _imageExporter = new SkiaImageExporter();
    private readonly ITiffEncoder _tiffEncoder = new LibTiffEncoder();
    private readonly ITiffValidator _tiffValidator = new LibTiffValidator();
    private readonly IPdfRasterizer _pdfRasterizer = new PdfToImagePdfRasterizer();
    private readonly IPdfService _pdfService;
    private readonly IBatchProcessingService _sut;

    private static readonly string OcrLanguageDataFolder = Path.Combine(Path.GetTempPath(), "scanova-ocr-test-langdata");
    private static readonly string TestFontPath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "NotoSans.ttf");

    /// <summary><see cref="IProgress{T}"/> que invoca o handler de forma síncrona (ao contrário de <see cref="Progress{T}"/>, que posta assincronamente) — necessário para testes determinísticos de progresso/pausa.</summary>
    private sealed class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;
        public SyncProgress(Action<T> handler) => _handler = handler;
        public void Report(T value) => _handler(value);
    }

    public BatchProcessingServiceTests()
    {
        _sourceDir = Path.Combine(Path.GetTempPath(), $"scanova-batch-src-{Guid.NewGuid():N}");
        _destDir = Path.Combine(Path.GetTempPath(), $"scanova-batch-dst-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_sourceDir);

        var imageService = new SkiaImageService();
        var detectionService = new DocumentDetectionService(imageService);
        var enhancementService = new DocumentEnhancementService(imageService, detectionService);
        var tiffPipeline = new SCANOVA.Tiff.TiffDocumentPipeline(imageService, _tiffEncoder, _tiffValidator);
        _pdfService = new PdfSharpPdfService(_tiffEncoder);
        var ocrService = new TesseractOcrService(_imageExporter, OcrLanguageDataFolder);

        _sut = new BatchProcessingService(_imageLoader, _imageExporter, enhancementService, _tiffEncoder, tiffPipeline, _pdfService, ocrService);
    }

    public void Dispose()
    {
        TryDeleteDirectory(_sourceDir);
        TryDeleteDirectory(_destDir);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best-effort — não falha o teste por causa de limpeza.
        }
    }

    /// <summary>Cria um arquivo PNG sintético em <see cref="_sourceDir"/> e devolve o caminho completo.</summary>
    private async Task<string> CreateSourcePngAsync(string name, byte r = 200, byte g = 200, byte b = 200)
    {
        const int size = 40;
        var stride = size * 4;
        var pixels = new byte[stride * size];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = r; pixels[i + 1] = g; pixels[i + 2] = b; pixels[i + 3] = 255;
        }

        var image = new RasterImage(size, size, stride, CorePixelFormat.Rgba32, pixels, 200, 200);
        var path = Path.Combine(_sourceDir, name);
        await _imageExporter.SaveAsync(image, path);
        return path;
    }

    /// <summary>Cria um PNG sintético de "documento escaneado" com texto real desenhado por código (fonte embutida, nunca um documento de uma pessoa real — seção 122), para o teste de OCR/PDF pesquisável.</summary>
    private async Task<string> CreateSourceTextPngAsync(string name, string text)
    {
        using var typeface = SKTypeface.FromFile(TestFontPath)
            ?? throw new InvalidOperationException($"Não foi possível carregar a fonte de teste em \"{TestFontPath}\".");

        const int width = 500;
        const int height = 120;
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        using var font = new SKFont(typeface, size: 36);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        canvas.DrawText(text, 20, 70, SKTextAlign.Left, font, paint);

        canvas.Flush();
        using var snapshot = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(snapshot);

        var stride = bitmap.RowBytes;
        var pixels = new byte[stride * bitmap.Height];
        System.Runtime.InteropServices.Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);

        var image = new RasterImage(width, height, stride, CorePixelFormat.Rgba32, pixels, 300, 300);
        var path = Path.Combine(_sourceDir, name);
        await _imageExporter.SaveAsync(image, path);
        return path;
    }

    private static BatchJob CreateJob(IReadOnlyList<string> sourcePaths, ExportSettings exportSettings, ImageAdjustments? adjustments = null) => new()
    {
        Id = Guid.NewGuid(),
        Items = sourcePaths.Select(p => new BatchItem { Id = Guid.NewGuid(), SourcePath = p }).ToList(),
        ExportSettings = exportSettings,
        Adjustments = adjustments ?? ImageAdjustments.None,
    };

    [Fact]
    public async Task RunAsync_MultipleTiffItems_AllSucceedWithCorrectStatus()
    {
        var paths = new[] { await CreateSourcePngAsync("a.png"), await CreateSourcePngAsync("b.png"), await CreateSourcePngAsync("c.png") };
        var job = CreateJob(paths, new ExportSettings { Format = OutputFormat.TiffDocumental, DestinationFolder = _destDir });

        var result = await _sut.RunAsync(job);

        Assert.Equal(ProcessingStatus.Completed, result.Status);
        Assert.Equal(3, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.All(result.Items, i =>
        {
            Assert.Equal(ProcessingStatus.Completed, i.Status);
            Assert.NotNull(i.OutputPath);
            Assert.True(File.Exists(i.OutputPath));
        });
    }

    [Fact]
    public async Task RunAsync_PreservesSourceFileNameStem()
    {
        var path = await CreateSourcePngAsync("recibo_042.png");
        var job = CreateJob(new[] { path }, new ExportSettings { Format = OutputFormat.TiffDocumental, DestinationFolder = _destDir });

        var result = await _sut.RunAsync(job);

        Assert.Equal("recibo_042.tif", Path.GetFileName(result.Items[0].OutputPath));
    }

    [Fact]
    public async Task RunAsync_JpgFormat_ProducesValidJpgFile()
    {
        var path = await CreateSourcePngAsync("foto.png");
        var job = CreateJob(new[] { path }, new ExportSettings { Format = OutputFormat.Jpg, DestinationFolder = _destDir });

        var result = await _sut.RunAsync(job);

        Assert.Equal(ProcessingStatus.Completed, result.Items[0].Status);
        var reloaded = await _imageLoader.LoadAsync(result.Items[0].OutputPath!);
        Assert.Equal(40, reloaded.Width);
    }

    [Fact]
    public async Task RunAsync_PdfFormat_ProducesOnePagePdfPerItem()
    {
        var paths = new[] { await CreateSourcePngAsync("p1.png"), await CreateSourcePngAsync("p2.png") };
        var job = CreateJob(paths, new ExportSettings { Format = OutputFormat.Pdf, DestinationFolder = _destDir });

        var result = await _sut.RunAsync(job);

        Assert.Equal(2, result.SuccessCount);
        foreach (var item in result.Items)
        {
            Assert.Equal(1, await _pdfRasterizer.GetPageCountAsync(item.OutputPath!));
        }
    }

    [Fact]
    public async Task RunAsync_ReportsProgressForEachItem()
    {
        var paths = new[] { await CreateSourcePngAsync("a.png"), await CreateSourcePngAsync("b.png") };
        var job = CreateJob(paths, new ExportSettings { Format = OutputFormat.Png, DestinationFolder = _destDir });

        var reports = new List<BatchProgress>();
        await _sut.RunAsync(job, new SyncProgress<BatchProgress>(reports.Add));

        Assert.Equal(2, reports.Count);
        Assert.Equal(1, reports[0].ProcessedCount);
        Assert.Equal(2, reports[1].ProcessedCount);
        Assert.All(reports, r => Assert.Equal(2, r.TotalCount));
    }

    [Fact]
    public async Task RunAsync_MissingSourceFile_MarksItemFailedAndContinuesOthers()
    {
        var goodPath = await CreateSourcePngAsync("good.png");
        var missingPath = Path.Combine(_sourceDir, "nao-existe.png");
        var job = CreateJob(new[] { missingPath, goodPath }, new ExportSettings { Format = OutputFormat.Png, DestinationFolder = _destDir });

        var result = await _sut.RunAsync(job);

        Assert.Equal(ProcessingStatus.Failed, result.Status); // ao menos um item falhou
        Assert.Equal(ProcessingStatus.Failed, result.Items[0].Status);
        Assert.NotNull(result.Items[0].ErrorMessage);
        Assert.Equal(ProcessingStatus.Completed, result.Items[1].Status); // o lote continuou
    }

    [Fact]
    public async Task RunAsync_PdfSearchableFormat_ProducesSearchablePdfWithRecognizedText()
    {
        // Fase 9: reconhece o texto (Tesseract de verdade) e gera um PDF com camada de texto
        // invisível — verificado re-extraindo o texto nativo do PDF gerado (PdfPig).
        var path = await CreateSourceTextPngAsync("recibo.png", "SCANOVA teste");
        var job = CreateJob(new[] { path }, new ExportSettings { Format = OutputFormat.PdfSearchable, DestinationFolder = _destDir });

        var result = await _sut.RunAsync(job);

        Assert.Equal(ProcessingStatus.Completed, result.Items[0].Status);
        Assert.True(File.Exists(result.Items[0].OutputPath));

        var extractedText = await _pdfService.TryExtractTextAsync(result.Items[0].OutputPath!, pageIndex: 0);
        Assert.NotNull(extractedText);
        Assert.Contains("SCANOVA", extractedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_PdfSearchableFormat_FailedOcrMarksOnlyThatItemFailed()
    {
        // Reaproveita o mesmo princípio de resiliência por item (seção 63): um arquivo de origem
        // ausente falha o OCR/PDF pesquisável desse item sem derrubar o lote inteiro.
        var missingPath = Path.Combine(_sourceDir, "nao-existe.png");
        var job = CreateJob(new[] { missingPath }, new ExportSettings { Format = OutputFormat.PdfSearchable, DestinationFolder = _destDir });

        var result = await _sut.RunAsync(job);

        Assert.Equal(ProcessingStatus.Failed, result.Items[0].Status);
        Assert.NotNull(result.Items[0].ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_OverwriteProtection_FailsWhenOutputAlreadyExistsAndPromptTrue()
    {
        var path = await CreateSourcePngAsync("dup.png");
        Directory.CreateDirectory(_destDir);
        await File.WriteAllTextAsync(Path.Combine(_destDir, "dup.tif"), "já existe");

        var job = CreateJob(new[] { path }, new ExportSettings { Format = OutputFormat.TiffDocumental, DestinationFolder = _destDir, PromptBeforeOverwrite = true });

        var result = await _sut.RunAsync(job);

        Assert.Equal(ProcessingStatus.Failed, result.Items[0].Status);
        Assert.Contains("já existe", result.Items[0].ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_OverwriteAllowed_WhenPromptBeforeOverwriteFalse()
    {
        var path = await CreateSourcePngAsync("dup.png");
        Directory.CreateDirectory(_destDir);
        await File.WriteAllTextAsync(Path.Combine(_destDir, "dup.tif"), "conteúdo antigo");

        var job = CreateJob(new[] { path }, new ExportSettings { Format = OutputFormat.TiffDocumental, DestinationFolder = _destDir, PromptBeforeOverwrite = false });

        var result = await _sut.RunAsync(job);

        Assert.Equal(ProcessingStatus.Completed, result.Items[0].Status);
    }

    [Fact]
    public async Task RunAsync_CreatesDestinationFolderAutomatically()
    {
        var path = await CreateSourcePngAsync("a.png");
        var nestedDest = Path.Combine(_destDir, "sub", "pasta");
        var job = CreateJob(new[] { path }, new ExportSettings { Format = OutputFormat.Png, DestinationFolder = nestedDest });

        var result = await _sut.RunAsync(job);

        Assert.Equal(ProcessingStatus.Completed, result.Items[0].Status);
        Assert.True(Directory.Exists(nestedDest));
    }

    [Fact]
    public async Task RunAsync_AppliesJobAdjustments()
    {
        var path = await CreateSourcePngAsync("color.png", r: 220, g: 40, b: 40);
        var job = CreateJob(new[] { path }, new ExportSettings { Format = OutputFormat.Png, DestinationFolder = _destDir }, new ImageAdjustments { ConvertToGrayscale = true });

        var result = await _sut.RunAsync(job);

        var reloaded = await _imageLoader.LoadAsync(result.Items[0].OutputPath!);
        // Depois de "escala de cinza", R=G=B em qualquer pixel (imagem recarregada como RGBA).
        Assert.Equal(reloaded.Pixels[0], reloaded.Pixels[1]);
        Assert.Equal(reloaded.Pixels[1], reloaded.Pixels[2]);
    }

    [Fact]
    public async Task RunAsync_CancelledBeforeStart_MarksAllItemsCancelled()
    {
        var paths = new[] { await CreateSourcePngAsync("a.png"), await CreateSourcePngAsync("b.png") };
        var job = CreateJob(paths, new ExportSettings { Format = OutputFormat.Png, DestinationFolder = _destDir });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => _sut.RunAsync(job, cancellationToken: cts.Token));

        Assert.Equal(ProcessingStatus.Cancelled, job.Status);
        Assert.All(job.Items, i => Assert.Equal(ProcessingStatus.Cancelled, i.Status));
    }

    [Fact]
    public async Task Pause_BlocksProcessingUntilResume()
    {
        var paths = new[] { await CreateSourcePngAsync("a.png"), await CreateSourcePngAsync("b.png") };
        var job = CreateJob(paths, new ExportSettings { Format = OutputFormat.Png, DestinationFolder = _destDir });

        _sut.Pause(job.Id); // pausa ANTES de iniciar — o lote deve travar antes do primeiro item.

        var runTask = _sut.RunAsync(job);

        await Task.Delay(150); // margem generosa para o laço alcançar o portão de pausa.
        Assert.False(runTask.IsCompleted);
        Assert.Equal(ProcessingStatus.Pending, job.Items[0].Status);

        _sut.Resume(job.Id);
        var result = await runTask;

        Assert.Equal(ProcessingStatus.Completed, result.Status);
        Assert.All(result.Items, i => Assert.Equal(ProcessingStatus.Completed, i.Status));
    }

    [Fact]
    public async Task RunAsync_CancelWhilePaused_MarksRemainingItemsCancelled()
    {
        var paths = new[] { await CreateSourcePngAsync("a.png"), await CreateSourcePngAsync("b.png") };
        var job = CreateJob(paths, new ExportSettings { Format = OutputFormat.Png, DestinationFolder = _destDir });

        using var cts = new CancellationTokenSource();
        _sut.Pause(job.Id);

        var runTask = _sut.RunAsync(job, cancellationToken: cts.Token);
        await Task.Delay(150);

        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => runTask);
        Assert.Equal(ProcessingStatus.Cancelled, job.Status);
        Assert.All(job.Items, i => Assert.Equal(ProcessingStatus.Cancelled, i.Status));
    }
}
#pragma warning restore CA1416
