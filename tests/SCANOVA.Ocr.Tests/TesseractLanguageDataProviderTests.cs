using SCANOVA.Core.Enums;
using SCANOVA.Ocr.Tesseract;
using Xunit;

namespace SCANOVA.Ocr.Tests;

/// <summary>
/// Testes da lógica pura (sem rede) do provedor de dados de idioma: mapeamento de código e
/// detecção de disponibilidade local a partir de uma pasta de cache isolada por teste.
/// O download de verdade (<see cref="TesseractLanguageDataProvider.EnsureAvailableAsync"/>
/// contra a rede) já é exercitado indiretamente pelos testes de <see cref="TesseractOcrServiceTests"/>.
/// </summary>
public class TesseractLanguageDataProviderTests
{
    [Theory]
    [InlineData(OcrLanguage.PortugueseBrazil, "por")]
    [InlineData(OcrLanguage.Portuguese, "por")]
    [InlineData(OcrLanguage.English, "eng")]
    [InlineData(OcrLanguage.Spanish, "spa")]
    [InlineData(OcrLanguage.Automatic, "por+eng")]
    public void CodeFor_KnownLanguage_ReturnsExpectedTesseractCode(OcrLanguage language, string expectedCode)
    {
        Assert.Equal(expectedCode, TesseractLanguageDataProvider.CodeFor(language));
    }

    [Fact]
    public void CodeFor_UnknownLanguage_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TesseractLanguageDataProvider.CodeFor((OcrLanguage)999));
    }

    private static string CreateEmptyCacheFolder() =>
        Directory.CreateTempSubdirectory("scanova-ocr-langdata-test-").FullName;

    [Fact]
    public void IsAvailableLocally_NoFilesCached_ReturnsFalse()
    {
        var provider = new TesseractLanguageDataProvider(CreateEmptyCacheFolder());

        Assert.False(provider.IsAvailableLocally(OcrLanguage.English));
    }

    [Fact]
    public void GetLocallyAvailableLanguages_NoFilesCached_ReturnsEmpty()
    {
        var provider = new TesseractLanguageDataProvider(CreateEmptyCacheFolder());

        Assert.Empty(provider.GetLocallyAvailableLanguages());
    }

    [Fact]
    public void IsAvailableLocally_TrainedDataFilePresent_ReturnsTrue()
    {
        var cacheFolder = CreateEmptyCacheFolder();
        File.WriteAllBytes(Path.Combine(cacheFolder, "eng.traineddata"), [1, 2, 3]);
        var provider = new TesseractLanguageDataProvider(cacheFolder);

        Assert.True(provider.IsAvailableLocally(OcrLanguage.English));
        Assert.Contains(OcrLanguage.English, provider.GetLocallyAvailableLanguages());
    }

    [Fact]
    public void IsAvailableLocally_AutomaticRequiresAllComponentLanguages()
    {
        var cacheFolder = CreateEmptyCacheFolder();
        File.WriteAllBytes(Path.Combine(cacheFolder, "por.traineddata"), [1]);
        var provider = new TesseractLanguageDataProvider(cacheFolder);

        // "Automatic" == "por+eng" — só "por" presente não é suficiente.
        Assert.False(provider.IsAvailableLocally(OcrLanguage.Automatic));

        File.WriteAllBytes(Path.Combine(cacheFolder, "eng.traineddata"), [1]);
        Assert.True(provider.IsAvailableLocally(OcrLanguage.Automatic));
    }

    [Fact]
    public void Constructor_CreatesCacheFolderIfMissing()
    {
        var parent = Directory.CreateTempSubdirectory("scanova-ocr-langdata-test-").FullName;
        var cacheFolder = Path.Combine(parent, "nested", "langdata");
        Assert.False(Directory.Exists(cacheFolder));

        _ = new TesseractLanguageDataProvider(cacheFolder);

        Assert.True(Directory.Exists(cacheFolder));
    }
}
