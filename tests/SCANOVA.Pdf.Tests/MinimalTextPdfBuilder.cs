using System.Text;

namespace SCANOVA.Pdf.Tests;

/// <summary>
/// Monta, byte a byte, um PDF de uma página com texto real (não uma imagem) — usado para testar
/// <c>IPdfService.TryExtractTextAsync</c> contra um PDF que já contém texto, sem depender de
/// nenhuma engine de renderização de fonte (referencia a fonte padrão Helvetica, embutida em
/// qualquer leitor de PDF por definição do próprio formato — não precisa de arquivo de fonte).
/// Evita usar o próprio PDFsharp para gerar esse PDF de teste porque desenhar texto via
/// XGraphics/XFont exige um <c>IFontResolver</c> configurado (não há fontes do sistema
/// garantidas neste ambiente de CI) — construir os bytes do PDF diretamente contorna esse
/// requisito por completo.
/// </summary>
internal static class MinimalTextPdfBuilder
{
    public static void WriteSinglePageWithText(string filePath, string text)
    {
        var buffer = new MemoryStream();
        var offsets = new List<long>();

        void WriteObject(int number, string content)
        {
            offsets.Add(buffer.Position);
            Append(buffer, $"{number} 0 obj\n{content}\nendobj\n");
        }

        Append(buffer, "%PDF-1.4\n");

        WriteObject(1, "<< /Type /Catalog /Pages 2 0 R >>");
        WriteObject(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        WriteObject(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>");

        var escaped = text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        var contentStream = $"BT /F1 14 Tf 10 50 Td ({escaped}) Tj ET";
        WriteObject(4, $"<< /Length {Encoding.ASCII.GetByteCount(contentStream)} >>\nstream\n{contentStream}\nendstream");

        WriteObject(5, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        var xrefOffset = buffer.Position;
        Append(buffer, $"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            Append(buffer, $"{offset:D10} 00000 n \n");
        }

        Append(buffer, $"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");

        File.WriteAllBytes(filePath, buffer.ToArray());
    }

    private static void Append(MemoryStream stream, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }
}
