using SCANOVA.Core.Enums;
using SCANOVA.Core.Models;
using Xunit;

namespace SCANOVA.Core.Tests;

/// <summary>Catálogo de perfis de digitalização (seção 11).</summary>
public class ScanProfileTests
{
    [Fact]
    public void Default_ContainsExpectedProfilesInOrder()
    {
        var names = ScanProfile.Default.Select(p => p.Name).ToArray();

        Assert.Equal(new[] { "TIFF Documental", "PDF Documental", "Documento Legível", "Colorido" }, names);
    }

    [Fact]
    public void TiffDocumental_Produces200DpiBlackAndWhite1Bit()
    {
        var profile = ScanProfile.Default.Single(p => p.Name == "TIFF Documental");
        var settings = profile.CreateSettings("scanner-1");

        Assert.Equal(200, settings.Dpi);
        Assert.Equal(ColorMode.BlackAndWhite1Bit, settings.ColorMode);
        Assert.Equal("scanner-1", settings.ScannerId);
    }

    [Fact]
    public void ReadableDocument_Produces300DpiGrayscale()
    {
        var profile = ScanProfile.Default.Single(p => p.Name == "Documento Legível");
        var settings = profile.CreateSettings("scanner-1");

        Assert.Equal(300, settings.Dpi);
        Assert.Equal(ColorMode.Grayscale, settings.ColorMode);
    }

    [Fact]
    public void Colored_Produces300DpiColor()
    {
        var profile = ScanProfile.Default.Single(p => p.Name == "Colorido");
        var settings = profile.CreateSettings("scanner-1");

        Assert.Equal(300, settings.Dpi);
        Assert.Equal(ColorMode.Color, settings.ColorMode);
    }
}
