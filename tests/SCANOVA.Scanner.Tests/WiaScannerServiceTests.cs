using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SCANOVA.Imaging.ImageLoading;
using SCANOVA.Scanner.Wia;
using Xunit;

namespace SCANOVA.Scanner.Tests;

/// <summary>
/// Testes de degradação graciosa do WiaScannerService fora do Windows — o cenário real deste
/// ambiente de CI (Linux). Não é possível testar a digitalização de verdade sem Windows +
/// scanner físico, mas o comportamento de "WIA indisponível" é testável e é exatamente o que a
/// especificação pede na seção 9 ("Nenhum scanner foi encontrado") quando não há WIA.
/// </summary>
#pragma warning disable CA1416 // Uso intencional de uma API Windows-only a partir de um contexto multiplataforma, para provar que ela degrada graciosamente (não lança) fora do Windows.
public class WiaScannerServiceTests
{
    private readonly IScannerService _sut = new WiaScannerService(new SkiaImageLoader());

    [Fact]
    public async Task DiscoverScannersAsync_WiaUnavailable_ReturnsEmptyListInsteadOfThrowing()
    {
        var scanners = await _sut.DiscoverScannersAsync();

        Assert.NotNull(scanners);
        Assert.Empty(scanners);
    }

    [Fact]
    public async Task ScanAsync_WiaUnavailable_ThrowsScannerExceptionWithFriendlyMessage()
    {
        var settings = ScanSettings.TiffDocumental("qualquer-id");

        var ex = await Assert.ThrowsAsync<ScannerException>(() => _sut.ScanAsync(settings));

        Assert.False(string.IsNullOrWhiteSpace(ex.UserMessage));
        Assert.DoesNotContain("Exception", ex.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DiscoverScannersAsync_RespectsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => _sut.DiscoverScannersAsync(cts.Token));
    }
}
#pragma warning restore CA1416
