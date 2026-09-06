using SCANOVA.Core.Enums;
using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SCANOVA.Imaging.ImageLoading;
using Xunit;

namespace SCANOVA.Ocr.Tests;

/// <summary>
/// Testes de ponta a ponta de verdade — o executável real do Tesseract (via
/// NAPS2.Tesseract.Binaries, Linux/Windows/macOS) reconhece texto desenhado por código em uma
/// imagem sintética. A pasta de modelos de idioma é fixa (não aleatória) para reaproveitar o
/// download entre execuções de teste nesta máquina, em vez de baixar ~2 MB a cada execução.
/// </summary>
public class TesseractOcrServiceTests
{
    private static readonly string LanguageDataFolder = Path.Combine(Path.GetTempPath(), "scanova-ocr-test-langdata");

    private readonly IOcrService _sut = new TesseractOcrService(new SkiaImageExporter(), LanguageDataFolder);

    [Fact]
    public async Task RecognizeAsync_ClearPrintedText_RecognizesWordsWithPositions()
    {
        var image = TestImages.CreateTextPage(900, 150, "Documento SCANOVA de teste");

        var result = await _sut.RecognizeAsync(image, OcrSettings.Default);

        Assert.Contains("SCANOVA", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("documento", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(result.Blocks);
        Assert.All(result.Blocks, b =>
        {
            Assert.True(b.BoundingBox.Width > 0);
            Assert.True(b.BoundingBox.Height > 0);
        });
    }

    [Fact]
    public async Task RecognizeAsync_MultipleLines_PreservesLineBreaksWhenRequested()
    {
        var image = TestImages.CreateTextPage(700, 200, "Primeira linha", "Segunda linha");
        var settings = new OcrSettings { PreserveLineBreaks = true };

        var result = await _sut.RecognizeAsync(image, settings);

        Assert.Contains('\n', result.Text);
    }

    [Fact]
    public async Task RecognizeAsync_BlankPage_ReturnsNearEmptyTextWithoutThrowing()
    {
        var image = TestImages.CreateBlankPage(400, 200);

        var result = await _sut.RecognizeAsync(image, OcrSettings.Default);

        Assert.True(string.IsNullOrWhiteSpace(result.Text));
        Assert.Empty(result.Blocks);
    }

    [Fact]
    public async Task RecognizeMultiPageAsync_TwoPages_ConcatenatesTextInOrder()
    {
        var page1 = TestImages.CreateTextPage(700, 150, "Pagina Um");
        var page2 = TestImages.CreateTextPage(700, 150, "Pagina Dois");

        var result = await _sut.RecognizeMultiPageAsync(new[] { page1, page2 }, OcrSettings.Default);

        var indexOfFirst = result.Text.IndexOf("Um", StringComparison.OrdinalIgnoreCase);
        var indexOfSecond = result.Text.IndexOf("Dois", StringComparison.OrdinalIgnoreCase);
        Assert.True(indexOfFirst >= 0 && indexOfSecond > indexOfFirst, $"Esperava \"Um\" antes de \"Dois\" no texto concatenado: {result.Text}");

        // Blocos de ambas as páginas devem estar presentes (a lista simplesmente concatena).
        Assert.True(result.Blocks.Count >= 2);
    }

    [Fact]
    public async Task GetAvailableLanguagesAsync_AfterRecognizing_IncludesLanguageUsed()
    {
        await _sut.RecognizeAsync(TestImages.CreateBlankPage(100, 100), OcrSettings.Default);

        var languages = await _sut.GetAvailableLanguagesAsync();

        Assert.Contains(OcrLanguage.PortugueseBrazil, languages);
    }

    [Fact]
    public async Task RecognizeAsync_CancelledBeforeStart_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _sut.RecognizeAsync(TestImages.CreateBlankPage(50, 50), OcrSettings.Default, cts.Token));
    }
}
