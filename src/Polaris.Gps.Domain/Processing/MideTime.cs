using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Domain.Profiles;

namespace Polaris.Gps.Domain.Processing;

/// <summary>
/// Walking time from distance and slope, with the MIDE method (Método de Información de Excursiones, also used by the
/// German and Swiss alpine clubs): 4 km/h on the flat, 400 m/h of ascent and 600 m/h of descent; the time of a stretch
/// is the larger of the horizontal and vertical times plus half of the smaller. It is applied to each leg between
/// points on the elevation smoothed as the ascent (<see cref="ProfileStatistics.Smooth"/>), so GPS noise does not add
/// time. A different flat speed scales the three rates alike. No stops are included.
/// </summary>
public static class MideTime
{
    public const double FlatKmh = 4;
    public const double AscentMetersPerHour = 400;
    public const double DescentMetersPerHour = 600;

    /// <summary>
    /// Seconds from the first point to each point, in order over all segments. Legs between segments count only when
    /// <paramref name="includeSegmentGaps"/>; legs with a point without elevation count only the horizontal time.
    /// </summary>
    /// <param name="flatKmh">Speed on the flat; <see cref="FlatKmh"/> is the MIDE reference.</param>
    public static double[] CumulativeSeconds(IReadOnlyList<IReadOnlyList<TrailPoint>> segments, double flatKmh = FlatKmh, bool includeSegmentGaps = true)
    {
        var scale = flatKmh / FlatKmh;
        var result = new List<double>();
        var total = 0.0;
        TrailPoint? previous = null;
        double? previousElevation = null;
        foreach (var segment in segments)
        {
            var smoothed = Smoothed(segment);
            for (var i = 0; i < segment.Count; i++)
            {
                if (previous is not null && (i > 0 || includeSegmentGaps))
                {
                    var hours = LegHours(Geodesy.Distance(previous.Position, segment[i].Position), previousElevation, smoothed[i], scale);
                    total += hours * 3600;
                }

                result.Add(total);
                previous = segment[i];
                previousElevation = smoothed[i];
            }
        }

        return [.. result];
    }

    /// <summary>MIDE time in hours of one leg: max(horizontal, vertical) + min(horizontal, vertical) / 2.</summary>
    public static double LegHours(double meters, double? fromElevation, double? toElevation, double scale = 1)
    {
        var horizontal = meters / (FlatKmh * 1000 * scale);
        var vertical = 0.0;
        if (fromElevation is { } a && toElevation is { } b)
        {
            vertical = b > a ? (b - a) / (AscentMetersPerHour * scale) : (a - b) / (DescentMetersPerHour * scale);
        }

        return Math.Max(horizontal, vertical) + (Math.Min(horizontal, vertical) / 2);
    }

    /// <summary>The smoothed elevation of each point of a segment, null where it has none.</summary>
    private static double?[] Smoothed(IReadOnlyList<TrailPoint> segment)
    {
        var result = new double?[segment.Count];
        var indexes = Enumerable.Range(0, segment.Count).Where(i => segment[i].Elevation is not null).ToList();
        if (indexes.Count < 2)
        {
            foreach (var i in indexes)
            {
                result[i] = segment[i].Elevation;
            }

            return result;
        }

        var distances = new List<double>(segment.Count) { 0 };
        for (var i = 1; i < segment.Count; i++)
        {
            distances.Add(distances[^1] + Geodesy.Distance(segment[i - 1].Position, segment[i].Position));
        }

        var values = indexes.Select(i => segment[i].Elevation!.Value).ToArray();
        var smooth = ProfileStatistics.Smooth(values, ProfileStatistics.WindowPoints(distances, values.Length));
        for (var k = 0; k < indexes.Count; k++)
        {
            result[indexes[k]] = smooth[k];
        }

        return result;
    }
}
