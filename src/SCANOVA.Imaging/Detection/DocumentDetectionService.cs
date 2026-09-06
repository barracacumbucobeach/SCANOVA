using SCANOVA.Core.Enums;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;

namespace SCANOVA.Imaging.Detection;

/// <summary>
/// Detecção automática do documento dentro de uma imagem digitalizada/fotografada (seção 13/17):
/// segmenta o documento do fundo por limiar de Otsu, extrai pontos do contorno externo, calcula o
/// fecho convexo (<see cref="ConvexHull"/>) e o retângulo de área mínima que o envolve
/// (<see cref="MinimumAreaRectangle"/>, algoritmo dos "rotating calipers") — um método clássico e
/// bem estabelecido, em vez de heurísticas ad hoc. O objetivo é localizar a região do documento,
/// nunca classificar juridicamente o seu conteúdo.
/// </summary>
public sealed class DocumentDetectionService : IDocumentDetectionService
{
    // Fração mínima/máxima da área da imagem que o documento detectado deve ocupar para ser
    // considerado plausível — evita "detectar" ruído minúsculo ou o quadro inteiro sem borda real.
    private const double MinAreaFraction = 0.02;
    private const double MaxAreaFraction = 0.995;
    private const double MinConfidenceToReport = 0.15;

    private readonly IImageService _imageService;

    public DocumentDetectionService(IImageService imageService)
    {
        _imageService = imageService;
    }

    public Task<DetectionResult> DetectAsync(RasterImage image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        return Task.Run(() => Detect(image), cancellationToken);
    }

    private DetectionResult Detect(RasterImage image)
    {
        var gray = image.Format == CorePixelFormat.Gray8 ? image : _imageService.ToGrayscale(image);
        var threshold = _imageService.ComputeOtsuThreshold(gray);
        var isForeground = ClassifyForeground(gray, threshold);

        var totalArea = (double)gray.Width * gray.Height;
        var boundaryPoints = new List<Point2D>();
        var topmostPerColumn = new int[gray.Width];
        var bottommostPerColumn = new int[gray.Width];
        Array.Fill(topmostPerColumn, -1);
        Array.Fill(bottommostPerColumn, -1);

        var foregroundCount = 0L;
        for (var y = 0; y < gray.Height; y++)
        {
            var rowOffset = y * gray.Stride;
            var leftX = -1;
            var rightX = -1;
            for (var x = 0; x < gray.Width; x++)
            {
                if (!isForeground(gray.Pixels[rowOffset + x]))
                {
                    continue;
                }

                foregroundCount++;
                if (leftX < 0) leftX = x;
                rightX = x;
                if (topmostPerColumn[x] < 0) topmostPerColumn[x] = y;
                bottommostPerColumn[x] = y;
            }

            if (leftX >= 0)
            {
                boundaryPoints.Add(new Point2D(leftX, y));
                boundaryPoints.Add(new Point2D(rightX, y));
            }
        }

        for (var x = 0; x < gray.Width; x++)
        {
            if (topmostPerColumn[x] < 0)
            {
                continue;
            }

            boundaryPoints.Add(new Point2D(x, topmostPerColumn[x]));
            boundaryPoints.Add(new Point2D(x, bottommostPerColumn[x]));
        }

        var areaFraction = foregroundCount / totalArea;
        if (boundaryPoints.Count < 3 || areaFraction < MinAreaFraction || areaFraction > MaxAreaFraction)
        {
            return NotFound();
        }

        var hull = ConvexHull.Compute(boundaryPoints);
        if (MinimumAreaRectangle.Compute(hull) is not { Area: > 0 } rect)
        {
            return NotFound();
        }

        var fillRatio = Math.Clamp(foregroundCount / rect.Area, 0.0, 1.0);
        if (fillRatio < MinConfidenceToReport)
        {
            return NotFound();
        }

        return new DetectionResult
        {
            DocumentFound = true,
            Region = ToQuad(rect),
            Confidence = fillRatio,
            SkewAngleDegrees = NormalizeAngle(rect.AngleDegrees),
            EstimatedSize = EstimateSize(rect.Width, rect.Height, image.HorizontalDpi, image.VerticalDpi),
        };
    }

    private static DetectionResult NotFound() => new()
    {
        DocumentFound = false,
        Confidence = 0.0,
    };

    /// <summary>
    /// Decide qual classe (acima ou abaixo do limiar) é o documento: a classe majoritária na
    /// borda da imagem é considerada o fundo; o documento é a classe oposta. Funciona tanto para
    /// documento claro sobre fundo escuro quanto o inverso, sem assumir a polaridade.
    /// </summary>
    /// <remarks>
    /// Usa a mesma convenção de <see cref="IImageService.ComputeOtsuThreshold"/>: a classe
    /// "inferior" é <c>v &lt;= threshold</c> e a "superior" é <c>v &gt; threshold</c> (não
    /// <c>&lt;</c>/<c>&gt;=</c>) — em uma imagem perfeitamente bimodal (duas cores sólidas), o
    /// limiar de Otsu pode cair exatamente no valor do cluster inferior (qualquer limiar dentro
    /// do "vale" entre os dois clusters maximiza igualmente a variância entre classes, e o laço
    /// de Otsu fica com o primeiro/mais à esquerda); usar o corte errado nesse caso classificaria
    /// as duas classes como uma só.
    /// </remarks>
    private static Func<byte, bool> ClassifyForeground(RasterImage gray, byte threshold)
    {
        long aboveCount = 0, belowCount = 0;

        void Sample(byte v)
        {
            if (v > threshold) aboveCount++; else belowCount++;
        }

        for (var x = 0; x < gray.Width; x++)
        {
            Sample(gray.Pixels[x]);
            Sample(gray.Pixels[(gray.Height - 1) * gray.Stride + x]);
        }

        for (var y = 0; y < gray.Height; y++)
        {
            Sample(gray.Pixels[y * gray.Stride]);
            Sample(gray.Pixels[y * gray.Stride + gray.Width - 1]);
        }

        var backgroundIsAboveThreshold = aboveCount >= belowCount;
        return backgroundIsAboveThreshold ? v => v <= threshold : v => v > threshold;
    }

    /// <summary>
    /// Ordena os quatro cantos do retângulo detectado em TopLeft→TopRight→BottomRight→BottomLeft.
    /// Ordena por ângulo polar ao redor do centroide (sempre dá a travessia correta do quadrilátero
    /// convexo) e escolhe como ponto de partida o canto de menor (x+y) — uma aproximação robusta de
    /// "canto superior esquerdo" para a faixa de rotação relevante ao deskew (tipicamente ±45°).
    /// </summary>
    private static CropRegion ToQuad(RotatedRect rect)
    {
        var corners = new[] { rect.Corner0, rect.Corner1, rect.Corner2, rect.Corner3 };
        var cx = corners.Average(p => p.X);
        var cy = corners.Average(p => p.Y);
        var ordered = corners.OrderBy(p => Math.Atan2(p.Y - cy, p.X - cx)).ToList();

        var startIndex = 0;
        var bestScore = double.MaxValue;
        for (var i = 0; i < ordered.Count; i++)
        {
            var score = ordered[i].X + ordered[i].Y;
            if (score < bestScore)
            {
                bestScore = score;
                startIndex = i;
            }
        }

        Point2D At(int offset) => ordered[(startIndex + offset) % ordered.Count];

        return new CropRegion
        {
            TopLeft = new DocumentPoint(At(0).X, At(0).Y),
            TopRight = new DocumentPoint(At(1).X, At(1).Y),
            BottomRight = new DocumentPoint(At(2).X, At(2).Y),
            BottomLeft = new DocumentPoint(At(3).X, At(3).Y),
        };
    }

    /// <summary>Normaliza um ângulo de aresta de retângulo (ambíguo módulo 90°) para o intervalo (-45, 45].</summary>
    private static double NormalizeAngle(double angleDegrees)
    {
        var a = angleDegrees % 90.0;
        if (a > 45) a -= 90;
        if (a <= -45) a += 90;
        return a;
    }

    private static DocumentSize EstimateSize(double widthPx, double heightPx, double hDpi, double vDpi)
    {
        if (hDpi <= 0 || vDpi <= 0)
        {
            return DocumentSize.Automatic;
        }

        var wIn = widthPx / hDpi;
        var hIn = heightPx / vDpi;
        var longIn = Math.Max(wIn, hIn);
        var shortIn = Math.Min(wIn, hIn);

        bool Matches(double longRef, double shortRef, double tolerance = 0.12) =>
            Math.Abs(longIn - longRef) <= longRef * tolerance && Math.Abs(shortIn - shortRef) <= shortRef * tolerance;

        if (Matches(11.69, 8.27)) return DocumentSize.A4;
        if (Matches(8.27, 5.83)) return DocumentSize.A5;

        var areaIn2 = wIn * hIn;
        if (areaIn2 < 8) return DocumentSize.Small;
        if (areaIn2 <= 40) return DocumentSize.Medium;
        return DocumentSize.Custom;
    }
}
