using SCANOVA.Core.Enums;

namespace SCANOVA.Core.Models;

/// <summary>Configuração de exportação/salvamento final de um documento processado.</summary>
public sealed class ExportSettings
{
    public required OutputFormat Format { get; init; }
    public required string DestinationFolder { get; init; }

    /// <summary>Nome de arquivo sem extensão. Quando nulo, é gerado a partir de <see cref="FileNamePattern"/>.</summary>
    public string? FileName { get; init; }

    /// <summary>Padrão para geração automática de nome (ex.: "Documento_{yyyyMMdd}_{HHmmss}").</summary>
    public string FileNamePattern { get; init; } = "Documento_{yyyyMMdd}_{HHmmss}";

    public bool CreateDateSubfolder { get; init; }
    public bool PromptBeforeOverwrite { get; init; } = true;

    public TiffSettings? Tiff { get; init; }
    public PdfSettings? Pdf { get; init; }

    /// <summary>Resolve o nome de arquivo (sem extensão) a ser usado, aplicando o padrão quando necessário.</summary>
    public string ResolveFileName(DateTime timestamp)
    {
        if (!string.IsNullOrWhiteSpace(FileName))
        {
            return FileName;
        }

        // Substitui os tokens {yyyyMMdd} e {HHmmss} (e variações de formato .NET entre chaves) pelo timestamp informado.
        return System.Text.RegularExpressions.Regex.Replace(
            FileNamePattern,
            @"\{([^{}]+)\}",
            match => timestamp.ToString(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
    }
}
