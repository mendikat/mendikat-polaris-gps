namespace Mendikat.TrailLab.Mapping.Local;

/// <summary>A calibration point: an image pixel and its coordinates in the map's coordinate system.</summary>
public sealed record ControlPoint(double PixelX, double PixelY, double X, double Y);

/// <summary>
/// Affine transformation from image pixels to map coordinates, fitted to the calibration points by least squares
/// (three or more points; with two, axis-aligned scale and offset). Pixel (0, 0) is the top-left corner of the image.
/// </summary>
public sealed class PixelTransform
{
    // X = A0 + A1·px + A2·py ; Y = B0 + B1·px + B2·py
    private readonly double _a0, _a1, _a2, _b0, _b1, _b2, _det;

    private PixelTransform(double a0, double a1, double a2, double b0, double b1, double b2)
    {
        (_a0, _a1, _a2, _b0, _b1, _b2) = (a0, a1, a2, b0, b1, b2);
        _det = (a1 * b2) - (a2 * b1);
    }

    /// <summary>Largest distance, in pixels, between a calibration point and where the fitted transform puts it.</summary>
    public double MaxResidualPixels { get; private init; }

    /// <exception cref="LocalMapException">Fewer than two points, or points that do not define a transformation.</exception>
    public static PixelTransform Fit(IReadOnlyList<ControlPoint> points)
    {
        if (points.Count < 2)
        {
            throw new LocalMapException(LocalMapError.InvalidCalibration, $"{points.Count} puntos de calibración (mínimo 2)");
        }

        var transform = points.Count == 2 || Collinear(points) ? FitAxisAligned(points) : FitAffine(points);
        if (!double.IsFinite(transform._det) || Math.Abs(transform._det) < 1e-12)
        {
            throw new LocalMapException(LocalMapError.InvalidCalibration, "puntos de calibración coincidentes o alineados");
        }

        var residual = points.Max(p =>
        {
            var (px, py) = transform.ToPixel(p.X, p.Y);
            return Math.Sqrt(((px - p.PixelX) * (px - p.PixelX)) + ((py - p.PixelY) * (py - p.PixelY)));
        });
        return new PixelTransform(transform._a0, transform._a1, transform._a2, transform._b0, transform._b1, transform._b2) { MaxResidualPixels = residual };
    }

    public (double X, double Y) ToMap(double pixelX, double pixelY) =>
        (_a0 + (_a1 * pixelX) + (_a2 * pixelY), _b0 + (_b1 * pixelX) + (_b2 * pixelY));

    public (double PixelX, double PixelY) ToPixel(double x, double y)
    {
        var dx = x - _a0;
        var dy = y - _b0;
        return (((_b2 * dx) - (_a2 * dy)) / _det, ((-_b1 * dx) + (_a1 * dy)) / _det);
    }

    /// <summary>Two points (or points on a line): x from the pixel column only, y from the pixel row only.</summary>
    private static PixelTransform FitAxisAligned(IReadOnlyList<ControlPoint> points)
    {
        var (first, last) = Farthest(points);
        var dpx = last.PixelX - first.PixelX;
        var dpy = last.PixelY - first.PixelY;
        if (Math.Abs(dpx) < 1e-9 || Math.Abs(dpy) < 1e-9)
        {
            throw new LocalMapException(LocalMapError.InvalidCalibration, "con dos puntos, deben estar en filas y columnas distintas");
        }

        var a1 = (last.X - first.X) / dpx;
        var b2 = (last.Y - first.Y) / dpy;
        return new PixelTransform(first.X - (a1 * first.PixelX), a1, 0, first.Y - (b2 * first.PixelY), 0, b2);
    }

    private static PixelTransform FitAffine(IReadOnlyList<ControlPoint> points)
    {
        // Normal equations of [1 px py] · [c0 c1 c2]ᵀ = value, solved for X and for Y. Pixels are centred first
        // for numerical stability.
        var mx = points.Average(p => p.PixelX);
        var my = points.Average(p => p.PixelY);
        double s11 = 0, s12 = 0, s22 = 0;
        double sx = 0, sxu = 0, sxv = 0, sy = 0, syu = 0, syv = 0;
        foreach (var p in points)
        {
            var u = p.PixelX - mx;
            var v = p.PixelY - my;
            s11 += u * u;
            s12 += u * v;
            s22 += v * v;
            sx += p.X;
            sxu += p.X * u;
            sxv += p.X * v;
            sy += p.Y;
            syu += p.Y * u;
            syv += p.Y * v;
        }

        var n = points.Count;
        var det = (s11 * s22) - (s12 * s12);
        var a1 = ((sxu * s22) - (sxv * s12)) / det;
        var a2 = ((sxv * s11) - (sxu * s12)) / det;
        var b1 = ((syu * s22) - (syv * s12)) / det;
        var b2 = ((syv * s11) - (syu * s12)) / det;
        var a0 = (sx / n) - (a1 * mx) - (a2 * my);
        var b0 = (sy / n) - (b1 * mx) - (b2 * my);
        return new PixelTransform(a0, a1, a2, b0, b1, b2);
    }

    private static bool Collinear(IReadOnlyList<ControlPoint> points)
    {
        var (first, last) = Farthest(points);
        var dx = last.PixelX - first.PixelX;
        var dy = last.PixelY - first.PixelY;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        return points.All(p => Math.Abs(((p.PixelX - first.PixelX) * dy) - ((p.PixelY - first.PixelY) * dx)) / length < 1);
    }

    private static (ControlPoint First, ControlPoint Last) Farthest(IReadOnlyList<ControlPoint> points)
    {
        var best = (points[0], points[1]);
        var bestDistance = -1.0;
        for (var i = 0; i < points.Count; i++)
        {
            for (var j = i + 1; j < points.Count; j++)
            {
                var d = Math.Pow(points[i].PixelX - points[j].PixelX, 2) + Math.Pow(points[i].PixelY - points[j].PixelY, 2);
                if (d > bestDistance)
                {
                    bestDistance = d;
                    best = (points[i], points[j]);
                }
            }
        }

        return best;
    }
}
