using SCANOVA.Core.Models;

namespace SCANOVA.Core.Interfaces;

/// <summary>
/// Reabre e valida um arquivo TIFF recém-gerado: verifica se é um TIFF válido e decodificável,
/// e confirma compressão, bits por amostra, resolução e dimensões (seção 26).
/// </summary>
public interface ITiffValidator
{
    Task<TiffValidationReport> ValidateAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>Valida especificamente contra o preset TIFF Documental (200 DPI, 1 bit, CCITT Group 4).</summary>
    Task<TiffValidationReport> ValidateDocumentalAsync(string filePath, CancellationToken cancellationToken = default);
}
