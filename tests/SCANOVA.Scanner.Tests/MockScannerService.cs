using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;

namespace SCANOVA.Scanner.Tests;

/// <summary>
/// Implementação mock de <see cref="IScannerService"/>, disponível apenas no ambiente de testes
/// (seção 140 da especificação: "Scanners podem ter uma implementação mock somente no ambiente
/// de testes" — nunca na aplicação final, que usa <see cref="Wia.WiaScannerService"/>).
/// </summary>
internal sealed class MockScannerService : IScannerService
{
    public List<ScannerInfo> Scanners { get; } = new()
    {
        new ScannerInfo { Id = "mock-scanner-1", Name = "Scanner de Teste", Manufacturer = "SCANOVA", IsDefault = true },
    };

    public Exception? NextScanException { get; set; }

    public ScanSettings? LastScanSettings { get; private set; }

    public Task<IReadOnlyList<ScannerInfo>> DiscoverScannersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult((IReadOnlyList<ScannerInfo>)Scanners);

    public Task<RasterImage> ScanAsync(ScanSettings settings, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        LastScanSettings = settings;

        if (NextScanException is not null)
        {
            throw NextScanException;
        }

        progress?.Report(0.5);
        var image = CreateSyntheticScan(settings);
        progress?.Report(1.0);
        return Task.FromResult(image);
    }

    private static RasterImage CreateSyntheticScan(ScanSettings settings)
    {
        const int width = 40;
        const int height = 30;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = pixels[i + 1] = pixels[i + 2] = 240;
            pixels[i + 3] = 255;
        }

        return new RasterImage(width, height, stride, CorePixelFormat.Rgba32, pixels, settings.Dpi, settings.Dpi);
    }
}
