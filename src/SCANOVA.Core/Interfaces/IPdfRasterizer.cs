using SCANOVA.Core.Models;

namespace SCANOVA.Core.Interfaces;

/// <summary>Rasteriza páginas de um PDF em <see cref="RasterImage"/>, na resolução informada.</summary>
public interface IPdfRasterizer
{
    Task<int> GetPageCountAsync(string pdfPath, CancellationToken cancellationToken = default);

    Task<RasterImage> RasterizePageAsync(string pdfPath, int pageIndex, double dpi, CancellationToken cancellationToken = default);

    IAsyncEnumerable<RasterImage> RasterizeAllAsync(string pdfPath, double dpi, CancellationToken cancellationToken = default);
}
