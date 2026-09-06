using SCANOVA.Core.Enums;
using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;

namespace SCANOVA.Tiff;

/// <summary>
/// Implementação de <see cref="ITiffDocumentPipeline"/>: escala de cinza → binarização →
/// normalização de DPI → CCITT Group 4 → validação (seção 84-86). Nota de implementação: a
/// normalização de DPI acontece sobre a imagem em escala de cinza, antes da binarização — não
/// exatamente a ordem textual da seção 85, mas produz bordas nitidamente melhores (reamostrar um
/// bitmap já binarizado sem suavização produziria serrilhado); o resultado final ainda satisfaz
/// todos os checkpoints da seção 85 (200 DPI, 1 bit, CCITT Group 4, validado).
/// </summary>
public sealed class TiffDocumentPipeline : ITiffDocumentPipeline
{
    private readonly IImageService _imageService;
    private readonly ITiffEncoder _encoder;
    private readonly ITiffValidator _validator;

    public TiffDocumentPipeline(IImageService imageService, ITiffEncoder encoder, ITiffValidator validator)
    {
        _imageService = imageService;
        _encoder = encoder;
        _validator = validator;
    }

    public async Task<ProcessingResult> SaveDocumentalTiffAsync(
        RasterImage source,
        string filePath,
        ImageAdjustments? adjustments = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        try
        {
            var prepared = await Task.Run(() => Prepare(source, adjustments ?? ImageAdjustments.None), cancellationToken)
                .ConfigureAwait(false);

            await _encoder.EncodeAsync(prepared, TiffSettings.Documental, filePath, cancellationToken).ConfigureAwait(false);

            return await ValidateAndBuildResultAsync(filePath, source, cancellationToken).ConfigureAwait(false);
        }
        catch (ScanovaException ex)
        {
            return ProcessingResult.Fail(ex.UserMessage, ex.Message);
        }
    }

    public async Task<ProcessingResult> SaveDocumentalTiffMultiPageAsync(
        IReadOnlyList<RasterImage> sources,
        string filePath,
        ImageAdjustments? adjustments = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Count == 0)
        {
            return ProcessingResult.Fail("Não há páginas para salvar.");
        }

        try
        {
            var resolvedAdjustments = adjustments ?? ImageAdjustments.None;
            var prepared = await Task.Run(() => sources.Select(s => Prepare(s, resolvedAdjustments)).ToList(), cancellationToken)
                .ConfigureAwait(false);

            await _encoder.EncodeMultiPageAsync(prepared, TiffSettings.Documental, filePath, cancellationToken).ConfigureAwait(false);

            return await ValidateAndBuildResultAsync(filePath, sources[0], cancellationToken).ConfigureAwait(false);
        }
        catch (ScanovaException ex)
        {
            return ProcessingResult.Fail(ex.UserMessage, ex.Message);
        }
    }

    private async Task<ProcessingResult> ValidateAndBuildResultAsync(string filePath, RasterImage source, CancellationToken cancellationToken)
    {
        var report = await _validator.ValidateDocumentalAsync(filePath, cancellationToken).ConfigureAwait(false);
        if (!report.IsValid)
        {
            return ProcessingResult.Fail(
                report.UserMessage ?? "Não foi possível validar o arquivo TIFF gerado. O arquivo original não foi alterado.",
                report.TechnicalDetail);
        }

        var outputSize = new FileInfo(filePath).Length;
        var originalSize = (long)source.Pixels.LongLength;

        return ProcessingResult.Ok(filePath, outputSize, originalSize);
    }

    private RasterImage Prepare(RasterImage source, ImageAdjustments adjustments)
    {
        var gray = source.Format == CorePixelFormat.Gray8 ? source : _imageService.ToGrayscale(source);

        var normalizedGray = _imageService.NormalizeDpi(gray, TiffSettings.Documental.HorizontalDpi);

        return adjustments.ThresholdMethod switch
        {
            ThresholdMethod.Adaptive => _imageService.BinarizeAdaptive(normalizedGray),
            ThresholdMethod.Global => _imageService.Binarize(normalizedGray, adjustments.GlobalThreshold),
            _ => _imageService.Binarize(normalizedGray, _imageService.ComputeOtsuThreshold(normalizedGray)),
        };
    }
}
