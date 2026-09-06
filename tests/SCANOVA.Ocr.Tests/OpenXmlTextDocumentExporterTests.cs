using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SCANOVA.Core.Interfaces;
using Xunit;

namespace SCANOVA.Ocr.Tests;

/// <summary>Testes de <see cref="OpenXmlTextDocumentExporter"/>: gravação e releitura de TXT/DOCX reais.</summary>
public class OpenXmlTextDocumentExporterTests
{
    private readonly ITextDocumentExporter _sut = new OpenXmlTextDocumentExporter();

    private static string TempFilePath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"scanova-ocr-export-test-{Guid.NewGuid():N}.{extension}");

    [Fact]
    public async Task SaveAsTextAsync_WritesExactTextAsUtf8()
    {
        var path = TempFilePath("txt");
        const string text = "Documento SCANOVA\nSegunda linha com acentuação: ç, ã, é";

        try
        {
            await _sut.SaveAsTextAsync(text, path);

            Assert.Equal(text, await File.ReadAllTextAsync(path, System.Text.Encoding.UTF8));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveAsTextAsync_MissingParentDirectory_CreatesItAutomatically()
    {
        var parent = Directory.CreateTempSubdirectory("scanova-ocr-export-test-").FullName;
        var path = Path.Combine(parent, "nested", "sub", "texto.txt");

        await _sut.SaveAsTextAsync("conteúdo", path);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task SaveAsDocxAsync_OnePerLine_CreatesOneParagraphPerLine()
    {
        var path = TempFilePath("docx");
        const string text = "Primeira linha\nSegunda linha\nTerceira linha";

        try
        {
            await _sut.SaveAsDocxAsync(text, path);

            using var document = WordprocessingDocument.Open(path, isEditable: false);
            var paragraphs = document.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().ToList();

            Assert.Equal(3, paragraphs.Count);
            Assert.Equal("Primeira linha", paragraphs[0].InnerText);
            Assert.Equal("Segunda linha", paragraphs[1].InnerText);
            Assert.Equal("Terceira linha", paragraphs[2].InnerText);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveAsDocxAsync_CrlfLineEndings_AreNormalizedBeforeSplitting()
    {
        var path = TempFilePath("docx");
        const string text = "Linha A\r\nLinha B";

        try
        {
            await _sut.SaveAsDocxAsync(text, path);

            using var document = WordprocessingDocument.Open(path, isEditable: false);
            var paragraphs = document.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().ToList();

            Assert.Equal(2, paragraphs.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveAsTextAsync_NullText_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.SaveAsTextAsync(null!, TempFilePath("txt")));
    }

    [Fact]
    public async Task SaveAsTextAsync_CancelledBeforeStart_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _sut.SaveAsTextAsync("texto", TempFilePath("txt"), cts.Token));
    }
}
