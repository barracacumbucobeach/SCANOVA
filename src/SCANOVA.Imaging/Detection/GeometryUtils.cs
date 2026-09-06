namespace SCANOVA.Imaging.Detection;

/// <summary>Ponto 2D usado internamente pelos algoritmos de geometria de detecção (sem dependência de <c>SCANOVA.Core</c>).</summary>
internal readonly record struct Point2D(double X, double Y);

/// <summary>
/// Fecho convexo pelo algoritmo de Andrew ("monotone chain", O(n log n)) — método clássico e
/// numericamente estável, preferível a heurísticas ad hoc para este problema.
/// </summary>
internal static class ConvexHull
{
    /// <summary>Retorna os pontos do fecho convexo, em ordem (sentido anti-horário, coordenadas padrão), sem duplicatas.</summary>
    public static IReadOnlyList<Point2D> Compute(IReadOnlyList<Point2D> points)
    {
        var sorted = points.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
        if (sorted.Count < 3)
        {
            return sorted;
        }

        var lower = new List<Point2D>();
        foreach (var p in sorted)
        {
            while (lower.Count >= 2 && Cross(lower[^2], lower[^1], p) <= 0)
            {
                lower.RemoveAt(lower.Count - 1);
            }

            lower.Add(p);
        }

        var upper = new List<Point2D>();
        for (var i = sorted.Count - 1; i >= 0; i--)
        {
            var p = sorted[i];
            while (upper.Count >= 2 && Cross(upper[^2], upper[^1], p) <= 0)
            {
                upper.RemoveAt(upper.Count - 1);
            }

            upper.Add(p);
        }

        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        lower.AddRange(upper);
        return lower;
    }

    private static double Cross(Point2D o, Point2D a, Point2D b) =>
        (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
}

/// <summary>Retângulo (possivelmente rotacionado) descrito por seus quatro cantos, na ordem em que foram calculados.</summary>
internal readonly record struct RotatedRect(Point2D Corner0, Point2D Corner1, Point2D Corner2, Point2D Corner3, double AngleDegrees, double Width, double Height)
{
    public double Area => Width * Height;
}

/// <summary>
/// Retângulo de área mínima que envolve um conjunto de pontos, pelo algoritmo dos "rotating
/// calipers" sobre o fecho convexo — o retângulo de área mínima sempre tem um lado colinear a
/// uma aresta do fecho convexo, então basta testar uma orientação por aresta (Toussaint, 1983).
/// </summary>
internal static class MinimumAreaRectangle
{
    public static RotatedRect? Compute(IReadOnlyList<Point2D> hull)
    {
        if (hull.Count == 0)
        {
            return null;
        }

        if (hull.Count == 1)
        {
            var p = hull[0];
            return new RotatedRect(p, p, p, p, 0, 0, 0);
        }

        RotatedRect? best = null;

        for (var i = 0; i < hull.Count; i++)
        {
            var a = hull[i];
            var b = hull[(i + 1) % hull.Count];
            if (a == b)
            {
                continue;
            }

            var edgeAngle = Math.Atan2(b.Y - a.Y, b.X - a.X);
            var cos = Math.Cos(edgeAngle);
            var sin = Math.Sin(edgeAngle);

            var minX = double.MaxValue;
            var maxX = double.MinValue;
            var minY = double.MaxValue;
            var maxY = double.MinValue;
            foreach (var p in hull)
            {
                // Rotaciona por -edgeAngle, alinhando esta aresta ao eixo X.
                var rx = p.X * cos + p.Y * sin;
                var ry = -p.X * sin + p.Y * cos;
                if (rx < minX) minX = rx;
                if (rx > maxX) maxX = rx;
                if (ry < minY) minY = ry;
                if (ry > maxY) maxY = ry;
            }

            var width = maxX - minX;
            var height = maxY - minY;
            var area = width * height;

            if (best is null || area < best.Value.Area)
            {
                // Rotaciona os quatro cantos (no espaço alinhado) de volta ao espaço original (+edgeAngle).
                Point2D Back(double rx, double ry) => new(rx * cos - ry * sin, rx * sin + ry * cos);

                best = new RotatedRect(
                    Back(minX, minY),
                    Back(maxX, minY),
                    Back(maxX, maxY),
                    Back(minX, maxY),
                    edgeAngle * 180.0 / Math.PI,
                    width,
                    height);
            }
        }

        return best;
    }
}
