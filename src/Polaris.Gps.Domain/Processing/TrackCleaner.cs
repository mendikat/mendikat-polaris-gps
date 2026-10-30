using Polaris.Gps.Domain.Editing;
using Polaris.Gps.Domain.Elevation;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Domain.Profiles;

namespace Polaris.Gps.Domain.Processing;

/// <summary>What the cleaning removes or changes; a null limit (or false) leaves that step out.</summary>
/// <param name="MaxSpeedKmh">A point reached from the previous kept one faster than this (km/h) is a GPS jump. Needs times.</param>
/// <param name="SpikeMeters">
/// A point more than this far (meters) from both neighbours, while the neighbours are close to each other (less than half
/// of either distance), is a spike: the line goes out and comes back.
/// </param>
/// <param name="StopRadiusMeters">Three or more consecutive points within this distance of the first are a stop: only its first and last points are kept.</param>
/// <param name="SmoothWindowMeters">
/// Elevations are replaced by a Gaussian moving average over a window of this width (meters), as the ascent of
/// mendikat.net (<see cref="ProfileStatistics.SmoothingWindow"/>, 100 m, by default in the dialog); null leaves them.
/// </param>
public sealed record CleaningOptions(double? MaxSpeedKmh, double? SpikeMeters, double? StopRadiusMeters, double? SmoothWindowMeters);

/// <summary>Preview of a cleaning and the command that applies it (null when nothing would change).</summary>
public sealed record CleaningPlan(int Total, int Jumps, int StopPoints, int Smoothed, IEditCommand? Command)
{
    public int Removed => Jumps + StopPoints;
}

/// <summary>
/// Cleans a recorded track or a route (Tools › Clean): removes GPS jumps and spikes, collapses stops and smooths the
/// elevation. Each segment is cleaned on its own; the first and last points of a segment are never removed. Everything
/// is one undoable step.
/// </summary>
public static class TrackCleaner
{
    public const double DefaultMaxSpeedKmh = 50;
    public const double DefaultSpikeMeters = 50;
    public const double DefaultStopRadiusMeters = 10;

    /// <summary>Stops need at least this many points; shorter groups are ordinary slow walking.</summary>
    private const int MinimumStopPoints = 3;

    /// <summary>Smoothed elevations closer than this to the original (meters) do not count as changed.</summary>
    private const double ElevationTolerance = 0.05;

    public static CleaningPlan Plan(IReadOnlyList<List<TrailPoint>> lists, CleaningOptions options)
    {
        var commands = new List<IEditCommand>();
        var elevations = new List<(TrailPoint Point, double Elevation)>();
        int total = 0, jumps = 0, stops = 0;
        foreach (var points in lists)
        {
            total += points.Count;
            var kept = RemoveJumps(points, options, ref jumps);
            kept = CollapseStops(kept, options.StopRadiusMeters, ref stops);
            if (kept.Count != points.Count)
            {
                commands.Add(new ReplaceItemsCommand<TrailPoint>(points, kept));
            }

            if (options.SmoothWindowMeters is { } window)
            {
                elevations.AddRange(SmoothedElevations(kept, window));
            }
        }

        if (elevations.Count > 0)
        {
            commands.Add(new SetElevationsCommand(elevations, ElevationSource.Smoothed));
        }

        return new CleaningPlan(total, jumps, stops, elevations.Count, commands.Count > 0 ? new CompositeCommand(commands) : null);
    }

    /// <summary>Jumps (too fast from the previous kept point) and spikes (out and back), in one pass.</summary>
    private static List<TrailPoint> RemoveJumps(List<TrailPoint> points, CleaningOptions options, ref int removed)
    {
        if (points.Count < 3 || (options.MaxSpeedKmh is null && options.SpikeMeters is null))
        {
            return [.. points];
        }

        var kept = new List<TrailPoint>(points.Count) { points[0] };
        for (var i = 1; i < points.Count - 1; i++)
        {
            var point = points[i];
            if (IsTooFast(kept[^1], point, options.MaxSpeedKmh) || IsSpike(kept[^1], point, points[i + 1], options.SpikeMeters))
            {
                removed++;
                continue;
            }

            kept.Add(point);
        }

        kept.Add(points[^1]);
        return kept;
    }

    private static bool IsTooFast(TrailPoint previous, TrailPoint point, double? maxSpeedKmh)
    {
        if (maxSpeedKmh is not { } max || previous.Time is not { } from || point.Time is not { } to || to <= from)
        {
            return false;
        }

        var speed = Geodesy.Distance(previous.Position, point.Position) / (to - from).TotalSeconds * 3.6;
        return speed > max;
    }

    private static bool IsSpike(TrailPoint previous, TrailPoint point, TrailPoint next, double? spikeMeters)
    {
        if (spikeMeters is not { } limit)
        {
            return false;
        }

        var outward = Geodesy.Distance(previous.Position, point.Position);
        var back = Geodesy.Distance(point.Position, next.Position);
        return outward > limit && back > limit && Geodesy.Distance(previous.Position, next.Position) < 0.5 * Math.Min(outward, back);
    }

    /// <summary>Keeps the first and last point of each group of <see cref="MinimumStopPoints"/> or more within the radius of its first point.</summary>
    private static List<TrailPoint> CollapseStops(List<TrailPoint> points, double? radius, ref int removed)
    {
        if (radius is not { } r || points.Count < MinimumStopPoints)
        {
            return points;
        }

        var kept = new List<TrailPoint>(points.Count);
        var start = 0;
        while (start < points.Count)
        {
            var end = start + 1;
            while (end < points.Count && Geodesy.Distance(points[start].Position, points[end].Position) <= r)
            {
                end++;
            }

            // points[start .. end - 1] are within the radius of points[start]
            var count = end - start;
            if (count >= MinimumStopPoints)
            {
                kept.Add(points[start]);
                kept.Add(points[end - 1]);
                removed += count - 2;
            }
            else
            {
                kept.AddRange(points.Skip(start).Take(count));
            }

            start = end;
        }

        return kept;
    }

    /// <summary>
    /// The elevations of the points that have one, smoothed as the profile does (<see cref="ProfileStatistics.Smooth"/>:
    /// Gaussian moving average over <paramref name="window"/> meters; 100 m in mendikat.net), rounded to decimetres; only those that change.
    /// </summary>
    private static IEnumerable<(TrailPoint Point, double Elevation)> SmoothedElevations(List<TrailPoint> points, double window)
    {
        var withElevation = points.Where(p => p.Elevation is not null).ToList();
        if (withElevation.Count < 2)
        {
            yield break;
        }

        var distances = new List<double>(points.Count) { 0 };
        for (var i = 1; i < points.Count; i++)
        {
            distances.Add(distances[^1] + Geodesy.Distance(points[i - 1].Position, points[i].Position));
        }

        var original = withElevation.Select(p => p.Elevation!.Value).ToArray();
        var smoothed = ProfileStatistics.Smooth(original, ProfileStatistics.WindowPoints(distances, original.Length, window));
        for (var i = 0; i < withElevation.Count; i++)
        {
            var value = Math.Round(smoothed[i], 1);
            if (Math.Abs(value - original[i]) >= ElevationTolerance)
            {
                yield return (withElevation[i], value);
            }
        }
    }
}
