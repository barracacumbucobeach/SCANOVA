namespace SCANOVA.Core.Enums;

/// <summary>Variante de exportação de PDF.</summary>
public enum PdfMode
{
    /// <summary>PDF documental: 200 DPI, preto e branco.</summary>
    Documental,

    Color,
    Grayscale,

    /// <summary>Imagem original + camada invisível de texto OCR (pesquisável).</summary>
    Searchable,
}
