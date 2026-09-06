using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using Xunit;

namespace SCANOVA.Scanner.Tests;

/// <summary>Sanidade do mock (seção 140) — usado para exercitar o contrato de IScannerService sem hardware.</summary>
public class MockScannerServiceTests
{
    [Fact]
    public async Task DiscoverScannersAsync_ReturnsConfiguredScanners()
    {
        IScannerService sut = new MockScannerService();

        var scanners = await sut.DiscoverScannersAsync();

        Assert.Single(scanners);
        Assert.True(scanners[0].IsDefault);
    }

    [Fact]
    public async Task ScanAsync_ReportsProgressAndReturnsImage()
    {
        IScannerService sut = new MockScannerService();
        var progressValues = new List<double>();
        var progress = new Progress<double>(progressValues.Add);

        var settings = ScanSettings.TiffDocumental("mock-scanner-1");
        var image = await sut.ScanAsync(settings, progress);

        Assert.NotNull(image);
        Assert.Equal(settings.Dpi, image.HorizontalDpi);
    }

    [Fact]
    public async Task ScanAsync_WhenConfiguredToFail_PropagatesScannerException()
    {
        var mock = new MockScannerService
        {
            NextScanException = new ScannerException("Scanner ocupado.", "teste"),
        };
        IScannerService sut = mock;

        var ex = await Assert.ThrowsAsync<ScannerException>(() =>
            sut.ScanAsync(ScanSettings.TiffDocumental("mock-scanner-1")));

        Assert.Equal("Scanner ocupado.", ex.UserMessage);
    }

    [Fact]
    public async Task ScanAsync_RecordsSettingsUsed()
    {
        var mock = new MockScannerService();
        var settings = ScanSettings.ReadableDocument("mock-scanner-1");

        await ((IScannerService)mock).ScanAsync(settings);

        Assert.Same(settings, mock.LastScanSettings);
    }
}
