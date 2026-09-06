using SCANOVA.Core.Enums;
using SCANOVA.Core.Models;
using Xunit;

namespace SCANOVA.Core.Tests;

/// <summary>
/// Testes de <see cref="ExportSettings.ResolveFileName"/> — a lógica de substituição de tokens
/// usada para nomear arquivos de "Salvar como" (não usada na conversão em lote, que preserva o
/// nome do arquivo de origem). Auditoria de cobertura da Fase 11: essa lógica não tinha nenhum
/// teste dedicado até então, apesar de ser lógica pura (sem I/O) e fácil de testar.
/// </summary>
public class ExportSettingsTests
{
    private static readonly DateTime Timestamp = new(2026, 3, 7, 14, 5, 9);

    private static ExportSettings CreateSettings(string? fileName = null, string? pattern = null) => new()
    {
        Format = OutputFormat.Pdf,
        DestinationFolder = "C:\\qualquer",
        FileName = fileName,
        FileNamePattern = pattern ?? "Documento_{yyyyMMdd}_{HHmmss}",
    };

    [Fact]
    public void ResolveFileName_ExplicitFileNameSet_IgnoresPattern()
    {
        var settings = CreateSettings(fileName: "recibo-042", pattern: "{yyyyMMdd}");

        Assert.Equal("recibo-042", settings.ResolveFileName(Timestamp));
    }

    [Fact]
    public void ResolveFileName_NoExplicitFileName_UsesDefaultPattern()
    {
        var settings = CreateSettings();

        Assert.Equal("Documento_20260307_140509", settings.ResolveFileName(Timestamp));
    }

    [Fact]
    public void ResolveFileName_CustomPattern_SubstitutesEachToken()
    {
        var settings = CreateSettings(pattern: "Scan-{yyyy}-{MM}-{dd}");

        Assert.Equal("Scan-2026-03-07", settings.ResolveFileName(Timestamp));
    }

    [Fact]
    public void ResolveFileName_PatternWithoutTokens_ReturnsItUnchanged()
    {
        var settings = CreateSettings(pattern: "documento-fixo");

        Assert.Equal("documento-fixo", settings.ResolveFileName(Timestamp));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveFileName_BlankExplicitFileName_FallsBackToPattern(string? blankFileName)
    {
        var settings = CreateSettings(fileName: blankFileName, pattern: "{yyyyMMdd}");

        Assert.Equal("20260307", settings.ResolveFileName(Timestamp));
    }
}
