using SCANOVA.Core.Enums;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SCANOVA.Imaging.Detection;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;

namespace SCANOVA.Imaging.Enhancement;

/// <summary>
/// Orquestra os primitivos de <see cref="IImageService"/> e a detecção de documento
/// (<see cref="IDocumentDetectionService"/>) em um pipeline completo de melhoria (seção 19/20):
/// perspectiva → inclinação → escala de cinza/remoção de fundo → redução de ruído → tom
/// (brilho/contraste/gamma) → nitidez → binarização + remoção de manchas. Cada etapa só roda
/// quando o respectivo ajuste é solicitado; edição não destrutiva (seção 45) — nunca modifica a
/// imagem recebida, sempre retorna uma nova.
/// </summary>
public sealed class DocumentEnhancementService : IDocumentEnhancementService
{
    private readonly IImageService _imageService;
    private readonly IDocumentDetectionService _detectionService;

    public DocumentEnhancementService(IImageService imageService, IDocumentDetectionService detectionService)
    {
        _imageService = imageService;
        _detectionService = detectionService;
    }

    public RasterImage Apply(RasterImage image, ImageAdjustments adjustments)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(adjustments);

        var current = image;

        if (adjustments.CorrectPerspective)
        {
            current = TryCorrectPerspective(current);
        }

        if (adjustments.CorrectSkew)
        {
            current = CorrectSkew(current);
        }

        // Remoção de fundo e binarização são operações de luminância — convertê-las para cinza é
        // um pré-requisito técnico, mesmo quando o usuário não marcou "escala de cinza" (seção 19).
        var willBecomeGrayscale = adjustments.ConvertToGrayscale || adjustments.RemoveBackground || adjustments.Binarize;

        if (!willBecomeGrayscale && adjustments.Saturation != 0 && current.Format is CorePixelFormat.Rgb24 or CorePixelFormat.Rgba32)
        {
            current = _imageService.AdjustSaturation(current, adjustments.Saturation);
        }

        if (willBecomeGrayscale)
        {
            current = _imageService.ToGrayscale(current);
        }

        if (adjustments.RemoveBackground)
        {
            current = _imageService.RemoveBackground(current);
        }

        if (adjustments.ReduceNoise)
        {
            current = _imageService.ReduceNoise(current);
        }

        if (adjustments.Brightness != 0 || adjustments.Contrast != 0)
        {
            current = _imageService.AdjustBrightnessContrast(current, adjustments.Brightness, adjustments.Contrast);
        }

        if (Math.Abs(adjustments.Gamma - 1.0) > 0.001)
        {
            current = _imageService.AdjustGamma(current, adjustments.Gamma);
        }

        if (adjustments.Sharpness > 0)
        {
            current = _imageService.Sharpen(current, adjustments.Sharpness / 50.0);
        }

        if (adjustments.Binarize)
        {
            current = adjustments.ThresholdMethod switch
            {
                ThresholdMethod.Adaptive => _imageService.BinarizeAdaptive(current),
                ThresholdMethod.Global => _imageService.Binarize(current, adjustments.GlobalThreshold),
                _ => _imageService.Binarize(current, _imageService.ComputeOtsuThreshold(current)),
            };

            current = BilevelDespeckle.Apply(current);
        }

        return current;
    }

    public ImageAdjustments ResolvePreset(EnhancementPreset preset) => preset switch
    {
        EnhancementPreset.Light => new ImageAdjustments
        {
            Contrast = 10,
            Sharpness = 15,
            CorrectSkew = true,
            CorrectPerspective = true,
        },
        EnhancementPreset.Normal => new ImageAdjustments
        {
            Contrast = 20,
            Gamma = 1.1,
            Sharpness = 25,
            ConvertToGrayscale = true,
            RemoveBackground = true,
            ReduceNoise = true,
            CorrectSkew = true,
            CorrectPerspective = true,
        },
        EnhancementPreset.Strong => new ImageAdjustments
        {
            Brightness = 5,
            Contrast = 35,
            Gamma = 1.3,
            Sharpness = 40,
            ConvertToGrayscale = true,
            RemoveBackground = true,
            ReduceNoise = true,
            CorrectSkew = true,
            CorrectPerspective = true,
        },
        // "Custom" significa que os valores concretos vêm dos controles manuais do usuário (modo
        // avançado, seção 20) — não deste resolvedor de presets. Um ponto de partida neutro é o
        // comportamento correto aqui; a UI nunca chama AutoEnhance com Custom esperando uma
        // melhoria automática de verdade, só usa este valor como base para os controles manuais.
        _ => ImageAdjustments.None,
    };

    public RasterImage AutoEnhance(RasterImage image, EnhancementPreset preset = EnhancementPreset.Normal) =>
        Apply(image, ResolvePreset(preset));

    /// <summary>
    /// Detecta o documento e, se encontrado, corrige a perspectiva/recorta para o quadrilátero
    /// detectado. Quando nada é detectado com confiança suficiente, mantém a imagem original —
    /// nunca aplica um corte arriscado a partir de uma detecção duvidosa.
    /// </summary>
    private RasterImage TryCorrectPerspective(RasterImage current)
    {
        // IDocumentDetectionService só expõe a forma assíncrona (contrato compartilhado com o
        // resto da aplicação). É seguro aguardar de forma síncrona aqui porque, por convenção
        // (docs/IMAGE_PROCESSING.md), quem chama Apply/AutoEnhance a partir da UI já despacha a
        // chamada inteira via Task.Run — não há thread de UI nem SynchronizationContext para travar.
        var detection = _detectionService.DetectAsync(current).GetAwaiter().GetResult();
        if (!detection.DocumentFound || detection.Region is not { } quad)
        {
            return current;
        }

        var outputWidth = Math.Max(1, (int)Math.Round((SideLength(quad.TopLeft, quad.TopRight) + SideLength(quad.BottomLeft, quad.BottomRight)) / 2.0));
        var outputHeight = Math.Max(1, (int)Math.Round((SideLength(quad.TopLeft, quad.BottomLeft) + SideLength(quad.TopRight, quad.BottomRight)) / 2.0));

        return _imageService.CorrectPerspective(current, quad, outputWidth, outputHeight);
    }

    private RasterImage CorrectSkew(RasterImage current)
    {
        var gray = current.Format == CorePixelFormat.Gray8 ? current : _imageService.ToGrayscale(current);
        var threshold = _imageService.ComputeOtsuThreshold(gray);
        var angleDegrees = ProjectionProfileSkewEstimator.EstimateSkewAngleDegrees(gray, threshold);

        if (Math.Abs(angleDegrees) < 0.1)
        {
            return current; // inclinação desprezível — evita reamostrar a imagem à toa.
        }

        return _imageService.Rotate(current, -angleDegrees);
    }

    private static double SideLength(DocumentPoint a, DocumentPoint b) =>
        Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
}
