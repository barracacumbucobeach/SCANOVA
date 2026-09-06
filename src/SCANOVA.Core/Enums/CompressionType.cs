namespace SCANOVA.Core.Enums;

/// <summary>Tipos de compressão suportados para saída TIFF.</summary>
public enum CompressionType
{
    /// <summary>Sem compressão.</summary>
    None,

    /// <summary>CCITT Group 4 (T.6) — exigido pelo preset TIFF Documental. Requer imagem bilevel (1 bit).</summary>
    CcittGroup4,

    /// <summary>CCITT Group 3 (T.4) — alternativa histórica ao Group 4.</summary>
    CcittGroup3,

    /// <summary>LZW — compressão sem perdas para imagens em cor/cinza.</summary>
    Lzw,

    /// <summary>Deflate/ZIP — compressão sem perdas para imagens em cor/cinza.</summary>
    Deflate,

    /// <summary>JPEG — compressão com perdas, não recomendada para o preset documental.</summary>
    Jpeg,
}
