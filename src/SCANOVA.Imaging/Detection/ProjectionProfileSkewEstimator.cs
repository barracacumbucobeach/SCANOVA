using SCANOVA.Core.Models;

namespace SCANOVA.Imaging.Detection;

/// <summary>
/// Estimador de inclinação (deskew) por perfil de projeção (método de Postl, 1986 — clássico e
/// amplamente usado para deskew de documentos digitalizados): testa um intervalo de ângulos
/// candidatos e escolhe aquele cuja projeção dos pixels de tinta sobre o eixo perpendicular às
/// linhas de texto é mais "concentrada" em faixas estreitas — linhas de texto bem alinhadas
/// produzem picos e vales pronunciados nessa projeção; texto ainda inclinado produz um perfil
/// raso e uniforme. Funciona mesmo sem nenhuma borda de página detectável (ex.: documento que
/// preenche todo o quadro, digitalizado por alimentador automático), ao contrário da estimativa
/// de inclinação feita a partir do retângulo de <see cref="DocumentDetectionService"/>.
/// </summary>
internal static class ProjectionProfileSkewEstimator
{
    /// <summary>
    /// Estima o ângulo de inclinação do texto, em graus. Um resultado positivo significa que o
    /// conteúdo está girado no sentido horário (mesma convenção de <c>IImageService.Rotate</c>);
    /// para endireitar a imagem, gire por <c>-ângulo</c> graus.
    /// </summary>
    public static double EstimateSkewAngleDegrees(
        RasterImage grayscaleImage,
        byte threshold,
        double maxAngleDegrees = 15.0,
        int maxSamplePoints = 20000)
    {
        var samples = SampleInkPixels(grayscaleImage, threshold, maxSamplePoints);
        if (samples.Count < 20)
        {
            // Pouca tinta amostrada para estimar com confiança — não arrisca uma "correção" que na
            // verdade seria apenas ruído.
            return 0.0;
        }

        var coarse = SearchBestAngle(samples, -maxAngleDegrees, maxAngleDegrees, 1.0);
        var fine = SearchBestAngle(samples, coarse - 1.0, coarse + 1.0, 0.1);
        return fine;
    }

    private static double SearchBestAngle(IReadOnlyList<Point2D> samples, double fromDegrees, double toDegrees, double stepDegrees)
    {
        var bestAngle = 0.0;
        var bestScore = double.MinValue;

        for (var angle = fromDegrees; angle <= toDegrees + 1e-9; angle += stepDegrees)
        {
            var score = ProfileVariance(samples, angle);
            if (score > bestScore)
            {
                bestScore = score;
                bestAngle = angle;
            }
        }

        return bestAngle;
    }

    /// <summary>
    /// Projeta cada ponto de tinta sobre o eixo Y após desfazer a rotação candidata, agrupa em
    /// faixas de 1px e retorna a variância das contagens por faixa — quanto maior, mais nítidos
    /// os "vales" entre linhas de texto (melhor alinhamento).
    /// </summary>
    private static double ProfileVariance(IReadOnlyList<Point2D> samples, double angleDegrees)
    {
        var angleRad = angleDegrees * Math.PI / 180.0;
        var cos = Math.Cos(angleRad);
        var sin = Math.Sin(angleRad);

        var bins = new Dictionary<int, int>();
        foreach (var p in samples)
        {
            var rotatedY = -p.X * sin + p.Y * cos;
            var bin = (int)Math.Floor(rotatedY);
            bins[bin] = bins.GetValueOrDefault(bin) + 1;
        }

        if (bins.Count == 0)
        {
            return 0.0;
        }

        var mean = bins.Values.Average();
        return bins.Values.Sum(c => (c - mean) * (c - mean)) / bins.Count;
    }

    private static List<Point2D> SampleInkPixels(RasterImage grayscaleImage, byte threshold, int maxSamplePoints)
    {
        var points = new List<Point2D>();
        var totalPixels = (long)grayscaleImage.Width * grayscaleImage.Height;
        // Passo de amostragem regular (não só os primeiros pixels encontrados) para manter no
        // máximo ~maxSamplePoints pontos sem viesar a amostra para uma região da imagem.
        var stride = Math.Max(1, (int)Math.Sqrt((double)totalPixels / Math.Max(1, maxSamplePoints)));

        for (var y = 0; y < grayscaleImage.Height; y += stride)
        {
            var rowOffset = y * grayscaleImage.Stride;
            for (var x = 0; x < grayscaleImage.Width; x += stride)
            {
                if (grayscaleImage.Pixels[rowOffset + x] < threshold)
                {
                    points.Add(new Point2D(x, y));
                }
            }
        }

        return points;
    }
}
