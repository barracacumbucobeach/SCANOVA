using SCANOVA.Ocr.Tesseract;
using Xunit;

namespace SCANOVA.Ocr.Tests;

/// <summary>
/// Testes do parser de hOCR usando um arquivo hOCR "à mão" (formato real de saída do Tesseract,
/// com <c>-c tessedit_create_hocr=1</c>) — evita depender de rodar o Tesseract de verdade para
/// verificar apenas a lógica de interpretação do XML.
/// </summary>
public class HocrParserTests
{
    private const string SampleHocr = """
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
        <html xmlns="http://www.w3.org/1999/xhtml" xml:lang="pt" lang="pt">
        <head>
        <title></title>
        <meta http-equiv="Content-Type" content="text/html;charset=utf-8"/>
        <meta name='ocr-system' content='tesseract 5.5.0' />
        <meta name='ocr-capabilities' content='ocr_page ocr_carea ocr_par ocr_line ocrx_word'/>
        </head>
        <body>
        <div class='ocr_page' id='page_1' title='image "input.png"; bbox 0 0 900 150; ppageno 0'>
        <div class='ocr_carea' id='block_1_1' title="bbox 40 20 800 130">
        <p class='ocr_par' id='par_1_1' lang='por' title="bbox 40 20 800 130">
        <span class='ocr_line' id='line_1_1' title="bbox 40 20 400 60; baseline 0 0; x_size 40">
        <span class='ocrx_word' id='word_1_1' title='bbox 40 20 200 60; x_wconf 95'>Documento</span>
        <span class='ocrx_word' id='word_1_2' title='bbox 210 20 400 60; x_wconf 90'>SCANOVA</span>
        </span>
        <span class='ocr_line' id='line_1_2' title="bbox 40 90 300 130; baseline 0 0; x_size 40">
        <span class='ocrx_word' id='word_1_3' title='bbox 40 90 300 130; x_wconf 88'>segunda</span>
        </span>
        </p>
        </div>
        </div>
        </body>
        </html>
        """;

    private static string WriteHocrFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"scanova-hocr-test-{Guid.NewGuid():N}.hocr");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Parse_SampleDocument_ExtractsWordsWithBoundingBoxesAndConfidence()
    {
        var path = WriteHocrFile(SampleHocr);
        try
        {
            var result = HocrParser.Parse(path, preserveLineBreaks: true);

            Assert.Equal(3, result.Blocks.Count);

            var first = result.Blocks[0];
            Assert.Equal("Documento", first.Text);
            Assert.Equal(new SCANOVA.Core.Models.BoundingBox(40, 20, 160, 40), first.BoundingBox);
            Assert.NotNull(first.Confidence);
            Assert.Equal(0.95, first.Confidence!.Value, precision: 3);

            var second = result.Blocks[1];
            Assert.Equal("SCANOVA", second.Text);

            var third = result.Blocks[2];
            Assert.Equal("segunda", third.Text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Parse_PreserveLineBreaksTrue_UsesNewlineBetweenLines()
    {
        var path = WriteHocrFile(SampleHocr);
        try
        {
            var result = HocrParser.Parse(path, preserveLineBreaks: true);

            Assert.Equal("Documento SCANOVA\nsegunda", result.Text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Parse_PreserveLineBreaksFalse_UsesSpaceBetweenLines()
    {
        var path = WriteHocrFile(SampleHocr);
        try
        {
            var result = HocrParser.Parse(path, preserveLineBreaks: false);

            Assert.Equal("Documento SCANOVA segunda", result.Text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Parse_OverallConfidence_IsAverageOfWordConfidences()
    {
        var path = WriteHocrFile(SampleHocr);
        try
        {
            var result = HocrParser.Parse(path, preserveLineBreaks: true);

            // (0.95 + 0.90 + 0.88) / 3
            Assert.NotNull(result.Confidence);
            Assert.Equal(0.91, result.Confidence!.Value, precision: 3);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Parse_PageWithNoWords_ReturnsEmptyResultWithNullConfidence()
    {
        const string emptyPageHocr = """
            <?xml version="1.0" encoding="UTF-8"?>
            <html xmlns="http://www.w3.org/1999/xhtml">
            <head><title></title></head>
            <body>
            <div class='ocr_page' id='page_1' title='image "input.png"; bbox 0 0 400 200; ppageno 0'>
            </div>
            </body>
            </html>
            """;

        var path = WriteHocrFile(emptyPageHocr);
        try
        {
            var result = HocrParser.Parse(path, preserveLineBreaks: true);

            Assert.Equal(string.Empty, result.Text);
            Assert.Empty(result.Blocks);
            Assert.Null(result.Confidence);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Parse_MissingFile_ThrowsOcrExceptionWithFriendlyMessage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"scanova-hocr-missing-{Guid.NewGuid():N}.hocr");

        var ex = Assert.Throws<SCANOVA.Core.Exceptions.OcrException>(() => HocrParser.Parse(path, preserveLineBreaks: true));

        Assert.False(string.IsNullOrWhiteSpace(ex.UserMessage));
    }
}
