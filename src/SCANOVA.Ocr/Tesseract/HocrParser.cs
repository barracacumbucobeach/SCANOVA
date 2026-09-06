using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Models;

namespace SCANOVA.Ocr.Tesseract;

/// <summary>
/// Extrai texto e posições de um arquivo hOCR (XHTML) gerado pelo Tesseract
/// (<c>-c tessedit_create_hocr=1</c>), na granularidade de palavra (classe <c>ocrx_word</c>) —
/// a mais útil tanto para alinhar a camada de texto pesquisável de um PDF (seção 109-111) quanto
/// para um editor de texto.
/// </summary>
internal static class HocrParser
{
    public static OcrResult Parse(string hocrFilePath, bool preserveLineBreaks)
    {
        XDocument document;
        try
        {
            // DtdProcessing.Ignore + XmlResolver nulo: o cabeçalho hOCR referencia um DTD
            // XHTML externo que não precisa (nem deve) ser buscado pela rede para interpretar o
            // conteúdo — só queremos os elementos e atributos.
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
            using var reader = XmlReader.Create(hocrFilePath, settings);
            document = XDocument.Load(reader);
        }
        catch (Exception ex)
        {
            throw new OcrException(
                "Não foi possível interpretar o resultado do reconhecimento de texto.",
                $"Falha ao carregar hOCR \"{hocrFilePath}\": {ex.Message}",
                ex);
        }

        var blocks = new List<OcrBlock>();
        var textBuilder = new StringBuilder();
        var isFirstLine = true;

        foreach (var line in document.Descendants().Where(e => HasClass(e, "ocr_line")))
        {
            if (!isFirstLine)
            {
                textBuilder.Append(preserveLineBreaks ? '\n' : ' ');
            }

            isFirstLine = false;
            var isFirstWordInLine = true;

            foreach (var word in line.Descendants().Where(e => HasClass(e, "ocrx_word")))
            {
                var text = word.Value.Trim();
                if (text.Length == 0 || !TryParseBoundingBox(word, out var box))
                {
                    continue;
                }

                blocks.Add(new OcrBlock { Text = text, BoundingBox = box, Confidence = TryParseWordConfidence(word) });

                if (!isFirstWordInLine)
                {
                    textBuilder.Append(' ');
                }

                isFirstWordInLine = false;
                textBuilder.Append(text);
            }
        }

        var overallConfidence = blocks.Count > 0 && blocks.All(b => b.Confidence.HasValue)
            ? blocks.Average(b => b.Confidence!.Value)
            : (double?)null;

        return new OcrResult { Text = textBuilder.ToString(), Confidence = overallConfidence, Blocks = blocks };
    }

    private static bool HasClass(XElement element, string className) =>
        ((string?)element.Attribute("class"))?.Split(' ').Contains(className) == true;

    /// <summary>Extrai "bbox x0 y0 x1 y1" do atributo <c>title</c> (formato hOCR: pares "chave valores" separados por ";").</summary>
    private static bool TryParseBoundingBox(XElement element, out BoundingBox box)
    {
        box = default;
        var title = (string?)element.Attribute("title");
        if (title is null)
        {
            return false;
        }

        foreach (var part in title.Split(';'))
        {
            var trimmed = part.Trim();
            if (!trimmed.StartsWith("bbox ", StringComparison.Ordinal))
            {
                continue;
            }

            var numbers = trimmed[5..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (numbers.Length != 4
                || !int.TryParse(numbers[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x0)
                || !int.TryParse(numbers[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y0)
                || !int.TryParse(numbers[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x1)
                || !int.TryParse(numbers[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y1))
            {
                continue;
            }

            box = new BoundingBox(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
            return true;
        }

        return false;
    }

    private static double? TryParseWordConfidence(XElement element)
    {
        var title = (string?)element.Attribute("title");
        if (title is null)
        {
            return null;
        }

        foreach (var part in title.Split(';'))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("x_wconf ", StringComparison.Ordinal)
                && double.TryParse(trimmed[8..], NumberStyles.Float, CultureInfo.InvariantCulture, out var confidence))
            {
                return Math.Clamp(confidence / 100.0, 0.0, 1.0);
            }
        }

        return null;
    }
}
