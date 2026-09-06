namespace SCANOVA.Core.Enums;

/// <summary>
/// Formatos de saída que o usuário pode escolher ao salvar/exportar um documento.
/// </summary>
public enum OutputFormat
{
    /// <summary>TIFF documental: 200 DPI, 1 bit, CCITT Group 4. Não permite cor.</summary>
    TiffDocumental,

    /// <summary>TIFF multipágina (várias páginas em um único arquivo .tif).</summary>
    TiffMultiPage,

    /// <summary>TIFF genérico, sem as restrições do preset documental (uso avançado).</summary>
    Tiff,

    Png,
    Jpg,

    /// <summary>PDF contendo apenas imagem (documental, colorido ou escala de cinza).</summary>
    Pdf,

    /// <summary>PDF com camada de texto OCR sobreposta à imagem original (pesquisável).</summary>
    PdfSearchable,
}
