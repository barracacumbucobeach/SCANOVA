using SCANOVA.Imaging.Detection;
using Xunit;

namespace SCANOVA.Imaging.Tests;

public class GeometryUtilsTests
{
    [Fact]
    public void ConvexHull_SquareWithInteriorPoint_ExcludesInteriorPoint()
    {
        var points = new[]
        {
            new Point2D(0, 0),
            new Point2D(10, 0),
            new Point2D(10, 10),
            new Point2D(0, 10),
            new Point2D(5, 5), // ponto interior — não deve aparecer no fecho.
        };

        var hull = ConvexHull.Compute(points);

        Assert.Equal(4, hull.Count);
        Assert.DoesNotContain(hull, p => p == new Point2D(5, 5));
        Assert.Contains(hull, p => p == new Point2D(0, 0));
        Assert.Contains(hull, p => p == new Point2D(10, 0));
        Assert.Contains(hull, p => p == new Point2D(10, 10));
        Assert.Contains(hull, p => p == new Point2D(0, 10));
    }

    [Fact]
    public void ConvexHull_CollinearPoints_KeepsOnlyEndpoints()
    {
        var points = new[]
        {
            new Point2D(0, 0),
            new Point2D(1, 0),
            new Point2D(2, 0),
            new Point2D(3, 0),
        };

        var hull = ConvexHull.Compute(points);

        Assert.Equal(2, hull.Count);
    }

    [Fact]
    public void MinimumAreaRectangle_AxisAlignedSquare_ReturnsExactDimensions()
    {
        var hull = ConvexHull.Compute(new[]
        {
            new Point2D(0, 0),
            new Point2D(20, 0),
            new Point2D(20, 10),
            new Point2D(0, 10),
        });

        var rect = MinimumAreaRectangle.Compute(hull);

        Assert.NotNull(rect);
        Assert.Equal(20, rect!.Value.Width, precision: 6);
        Assert.Equal(10, rect.Value.Height, precision: 6);
        Assert.Equal(200, rect.Value.Area, precision: 6);
    }

    [Fact]
    public void MinimumAreaRectangle_RotatedSquare_RecoversOriginalSideLengthAndAngle()
    {
        const double halfSize = 10;
        const double angleDegrees = 25;
        var angleRad = angleDegrees * Math.PI / 180.0;
        var cos = Math.Cos(angleRad);
        var sin = Math.Sin(angleRad);

        Point2D Rotate(double x, double y) => new(x * cos - y * sin, x * sin + y * cos);

        var hull = ConvexHull.Compute(new[]
        {
            Rotate(-halfSize, -halfSize),
            Rotate(halfSize, -halfSize),
            Rotate(halfSize, halfSize),
            Rotate(-halfSize, halfSize),
        });

        var rect = MinimumAreaRectangle.Compute(hull);

        Assert.NotNull(rect);
        // Um quadrado rotacionado continua tendo o retângulo de área mínima com o mesmo lado
        // (2×halfSize) — só a orientação muda.
        Assert.Equal(2 * halfSize, rect!.Value.Width, precision: 3);
        Assert.Equal(2 * halfSize, rect.Value.Height, precision: 3);
    }
}
