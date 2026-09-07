using SCANOVA.Core.Models;

namespace SCANOVA.Core.Interfaces;

/// <summary>
/// Abstração de aquisição de imagens via scanner. A primeira implementação usa WIA (Windows
/// Image Acquisition); esta interface permite substituir/adicionar outras tecnologias no futuro
/// sem alterar a UI (seção 9).
/// </summary>
public interface IScannerService
{
    /// <summary>Lista os scanners atualmente detectados/instalados no sistema.</summary>
    Task<IReadOnlyList<ScannerInfo>> DiscoverScannersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Digitaliza usando as configurações informadas. Não bloqueia a UI thread; relata progresso
    /// através de <paramref name="progress"/> quando a origem suportar. Lança
    /// <see cref="Exceptions.ScannerException"/> em caso de falha (scanner desconectado, ocupado, etc.).
    /// </summary>
    Task<RasterImage> ScanAsync(ScanSettings settings, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}
