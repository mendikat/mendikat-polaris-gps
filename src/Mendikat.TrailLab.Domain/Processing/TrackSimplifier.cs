using GeographicLib;
using GeographicLib.Projections;
using Mendikat.TrailLab.Domain.Editing;
using Mendikat.TrailLab.Domain.Model;

namespace Mendikat.TrailLab.Domain.Processing;

/// <summary>Result of planning a simplification: counts for the preview and the undoable command (SIMP-04).</summary>
public sealed record SimplificationPlan(int OriginalCount, int ResultCount, double ToleranceMeters, IEditCommand? Command)
{
    public int RemovedCount => OriginalCount - ResultCount;
}

/// <summary>
/// Douglas–Peucker simplification with a tolerance in meters (SIMP-01, ADR-0008). Each segment is processed on its
/// own in a local Transverse Mercator projection centred on the segment (scale error below 0.1 % within ~250 km of
/// its mean longitude). The first and last point of every segment are always kept and segments are never merged
/// (SIMP-02). Kept points are not modified (SIMP-03). Iterative, so very long tracks cannot overflow the stack (SIMP-05).
/// </summary>
public static class TrackSimplifier
{
    private static readonly TransverseMercator Projection = new(Geodesic.WGS84, 1.0);

    /// <param name="segments">The point lists to simplify (track segments, or one route list).</param>
    public static SimplificationPlan Plan(IReadOnlyList<List<TrailPoint>> segments, double toleranceMeters)
    {
        if (!double.IsFinite(toleranceMeters) || toleranceMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(toleranceMeters), toleranceMeters, "Tolerance must be positive.");
        }

        var commands = new List<IEditCommand>();
        var original = 0;
        var result = 0;
        foreach (var segment in segments)
        {
            var kept = KeptIndices(segment, toleranceMeters);
            original += segment.Count;
            result += kept.Count;
            if (kept.Count < segment.Count)
            {
                var keptSet = kept.ToHashSet();
                commands.Add(new RemovePointsCommand(segment, segment.Where((_, i) => !keptSet.Contains(i))));
            }
        }

        var command = commands.Count switch
        {
            0 => null,
            1 => commands[0],
            _ => new CompositeCommand(commands),
        };
        return new SimplificationPlan(original, result, toleranceMeters, command);
    }

    /// <summary>Indices (ascending) of the points of one segment that Douglas–Peucker keeps.</summary>
    public static List<int> KeptIndices(IReadOnlyList<TrailPoint> points, double toleranceMeters)
    {
        var count = points.Count;
        if (count <= 2)
        {
            return Enumerable.Range(0, count).ToList();
        }

        var (xs, ys) = Project(points);
        var keep = new bool[count];
        keep[0] = keep[count - 1] = true;
        var stack = new Stack<(int First, int Last)>();
        stack.Push((0, count - 1));

        while (stack.Count > 0)
        {
            var (first, last) = stack.Pop();
            var farthest = -1;
            var maxDistance = toleranceMeters;
            for (var i = first + 1; i < last; i++)
            {
                var distance = DistanceToSegment(xs[i], ys[i], xs[first], ys[first], xs[last], ys[last]);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    farthest = i;
                }
            }

            if (farthest >= 0)
            {
                keep[farthest] = true;
                stack.Push((first, farthest));
                stack.Push((farthest, last));
            }
        }

        var kept = new List<int>();
        for (var i = 0; i < count; i++)
        {
            if (keep[i])
            {
                kept.Add(i);
            }
        }

        return kept;
    }

    /// <summary>Planar coordinates in meters, Transverse Mercator centred on the segment's mean longitude.</summary>
    private static (double[] X, double[] Y) Project(IReadOnlyList<TrailPoint> points)
    {
        // Longitudes are unwrapped relative to the first point so a segment crossing ±180° is still contiguous.
        var reference = points[0].Position.Longitude;
        double Unwrap(double lon) => reference + (((lon - reference) % 360 + 540) % 360 - 180);
        var centralMeridian = points.Average(p => Unwrap(p.Position.Longitude));

        var xs = new double[points.Count];
        var ys = new double[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            (xs[i], ys[i]) = Projection.Forward(centralMeridian, points[i].Position.Latitude, Unwrap(points[i].Position.Longitude));
        }

        return (xs, ys);
    }

    /// <summary>Distance from P to the segment AB (to A if A and B coincide, e.g. a closed loop).</summary>
    private static double DistanceToSegment(double px, double py, double ax, double ay, double bx, double by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        var lengthSquared = dx * dx + dy * dy;
        var t = lengthSquared == 0 ? 0 : Math.Clamp(((px - ax) * dx + (py - ay) * dy) / lengthSquared, 0, 1);
        var cx = ax + t * dx - px;
        var cy = ay + t * dy - py;
        return Math.Sqrt(cx * cx + cy * cy);
    }
}
