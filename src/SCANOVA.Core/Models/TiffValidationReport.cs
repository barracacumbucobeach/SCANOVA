using SCANOVA.Core.Enums;

namespace SCANOVA.Core.Models;

/// <summary>
/// Resultado da validação de um arquivo TIFF já salvo em disco (ver <c>ITiffValidator</c> e seção 26).
/// Corresponde ao checklist exibido ao usuário: TIFF válido, DPI, bits por pixel, compressão, arquivo legível.
/// É um <c>record</c> para permitir criar variações com <c>with</c> (ex.: marcar inválido ao não
/// satisfazer o preset documental, preservando os demais campos já lidos do arquivo).
/// </summary>
public sealed record TiffValidationReport
{
    public required bool IsValid { get; init; }
    public required bool IsReadableTiff { get; init; }
    public required bool IsDecodable { get; init; }

    public double? HorizontalDpi { get; init; }
    public double? VerticalDpi { get; init; }
    public int? BitsPerSample { get; init; }
    public CompressionType? Compression { get; init; }

    /// <summary>Verdadeiro quando a tag ResolutionUnit do arquivo é "polegada" (seção 25/123).</summary>
    public bool? ResolutionUnitIsInch { get; init; }

    public int? Width { get; init; }
    public int? Height { get; init; }
    public int PageCount { get; init; }

    public bool MatchesDpi(double expected) => HorizontalDpi == expected && VerticalDpi == expected;
    public bool Is1Bit => BitsPerSample == 1;
    public bool IsCcittGroup4 => Compression == CompressionType.CcittGroup4;

    /// <summary>Mensagem amigável, preenchida quando a validação falha (seção 26).</summary>
    public string? UserMessage { get; init; }

    public string? TechnicalDetail { get; init; }
}
