using SCANOVA.Core.Enums;

namespace SCANOVA.Core.Models;

/// <summary>
/// Configuração de geração de um arquivo TIFF. <see cref="Documental"/> representa o preset
/// obrigatório do produto (200 DPI, 1 bit, CCITT Group 4) e não deve ser alterado silenciosamente:
/// qualquer configuração que quebre essas regras deixa de ser "documental" e deve ser apresentada
/// ao usuário como uma escolha explícita de sair do modo padrão (ver seção 49 da especificação).
/// </summary>
public sealed class TiffSettings
{
    public required double HorizontalDpi { get; init; }
    public required double VerticalDpi { get; init; }
    public required ColorMode ColorMode { get; init; }
    public required CompressionType Compression { get; init; }

    /// <summary>Gera um único arquivo TIFF com várias páginas quando o documento tiver mais de uma página.</summary>
    public bool MultiPage { get; init; }

    /// <summary>Reexecuta o <c>ITiffValidator</c> imediatamente após salvar.</summary>
    public bool ValidateAfterSave { get; init; } = true;

    /// <summary>
    /// Preset comercial padrão do SCANOVA: 200 DPI, preto e branco 1-bit, CCITT Group 4.
    /// Não é permitido salvar TIFF colorido usando este preset (ver seção 22).
    /// </summary>
    public static TiffSettings Documental { get; } = new()
    {
        HorizontalDpi = 200,
        VerticalDpi = 200,
        ColorMode = ColorMode.BlackAndWhite1Bit,
        Compression = CompressionType.CcittGroup4,
        ValidateAfterSave = true,
    };

    /// <summary>Verdadeiro quando esta configuração satisfaz integralmente o preset <see cref="Documental"/>.</summary>
    public bool SatisfiesDocumentalPreset =>
        HorizontalDpi == Documental.HorizontalDpi &&
        VerticalDpi == Documental.VerticalDpi &&
        ColorMode == Documental.ColorMode &&
        Compression == Documental.Compression;
}
