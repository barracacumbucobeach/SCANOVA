using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;

namespace SCANOVA.Imaging.Composition;

/// <summary>
/// Implementação de <see cref="IDuplexCompositionService"/>: pura reordenação/rotação de uma
/// lista já em memória — nenhuma operação de pixel além do giro opcional de 180° (reaproveita
/// <see cref="IImageService.Rotate"/>, já testado).
/// </summary>
public sealed class DuplexCompositionService : IDuplexCompositionService
{
    private readonly IImageService _imageService;

    public DuplexCompositionService(IImageService imageService)
    {
        _imageService = imageService;
    }

    public IReadOnlyList<RasterImage> Compose(
        IReadOnlyList<RasterImage> frontPages,
        IReadOnlyList<RasterImage> backPages,
        DuplexCompositionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(frontPages);
        ArgumentNullException.ThrowIfNull(backPages);

        if (frontPages.Count == 0 || backPages.Count == 0)
        {
            throw new DuplexCompositionException(
                "É preciso escanear ou abrir pelo menos uma página de frente e uma de verso.",
                $"Compose chamado com {frontPages.Count} frente(s) e {backPages.Count} verso(s).");
        }

        if (frontPages.Count != backPages.Count)
        {
            throw new DuplexCompositionException(
                $"O número de páginas de frente ({frontPages.Count}) e de verso ({backPages.Count}) precisa ser igual.",
                $"Compose chamado com {frontPages.Count} frente(s) e {backPages.Count} verso(s) — contagens diferentes.");
        }

        var opts = options ?? DuplexCompositionOptions.Default;

        // ToList() em vez de Reverse() in-place: nunca modifica a lista recebida pelo chamador.
        var orderedBacks = opts.ReverseBackOrder ? backPages.Reverse().ToList() : backPages;

        var result = new List<RasterImage>(frontPages.Count * 2);
        for (var i = 0; i < frontPages.Count; i++)
        {
            result.Add(frontPages[i]);

            var back = orderedBacks[i];
            if (opts.RotateBackPages180)
            {
                back = _imageService.Rotate(back, 180);
            }

            result.Add(back);
        }

        return result;
    }
}
