using Polaris.Gps.Domain.Editing;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;

namespace Polaris.Gps.Domain.Processing;

public enum SpeedUnit
{
    KilometersPerHour,
    MetersPerSecond,

    /// <summary>Pace in minutes per kilometer.</summary>
    MinutesPerKilometer,
}

public enum TimeMode
{
    /// <summary>t = t0 + distance / speed: times follow the real distance between points (TIME-02).</summary>
    ByDistance,

    /// <summary>t = t0 + i · interval: strictly uniform spacing, regardless of distance (TIME-03).</summary>
    UniformInterval,
}

public enum TimeStartIssue
{
    None,

    /// <summary>The local start time falls in a daylight-saving gap and does not exist.</summary>
    Invalid,

    /// <summary>The local start time happens twice (end of daylight saving); standard time is used.</summary>
    Ambiguous,
}

/// <param name="StartLocal">Start date and time as the user typed it, in <paramref name="TimeZone"/> (no conversion yet).</param>
/// <param name="Speed">Speed in <paramref name="Unit"/>; used in <see cref="TimeMode.ByDistance"/>.</param>
/// <param name="Interval">Time between points; used in <see cref="TimeMode.UniformInterval"/>.</param>
/// <param name="IncludeSegmentGaps">Count the distance between segments at the same speed (TIME-04, default true).</param>
public sealed record TimeOptions(
    DateTime StartLocal,
    TimeZoneInfo TimeZone,
    TimeMode Mode,
    double Speed,
    SpeedUnit Unit,
    TimeSpan Interval,
    bool IncludeSegmentGaps = true);

/// <summary>Result of planning new times: preview data and the undoable command.</summary>
public sealed record TimePlan(
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int PointCount,
    int PointsWithPreviousTime,
    TimeStartIssue StartIssue,
    IReadOnlyList<(TrailPoint Point, DateTimeOffset Time)> Times,
    IEditCommand Command)
{
    public TimeSpan Duration => EndUtc - StartUtc;
}

/// <summary>
/// Regenerates the times of a track or route (TIME-01..06, ADR-0008). The local start time is converted to UTC once;
/// all times are stored in UTC. Only the given points change; waypoints and other documents are never touched.
/// </summary>
public static class TimeRegenerator
{
    public static double ToMetersPerSecond(double speed, SpeedUnit unit) => unit switch
    {
        SpeedUnit.KilometersPerHour => speed / 3.6,
        SpeedUnit.MetersPerSecond => speed,
        SpeedUnit.MinutesPerKilometer => speed > 0 ? 1000 / (speed * 60) : 0,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null),
    };

    public static TimeStartIssue CheckStart(DateTime startLocal, TimeZoneInfo zone)
    {
        var unspecified = DateTime.SpecifyKind(startLocal, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(unspecified))
        {
            return TimeStartIssue.Invalid;
        }

        return zone.IsAmbiguousTime(unspecified) ? TimeStartIssue.Ambiguous : TimeStartIssue.None;
    }

    /// <summary>Plans the new times; null if there are no points or the options are invalid (speed/interval ≤ 0, nonexistent start).</summary>
    public static TimePlan? Plan(IReadOnlyList<IReadOnlyList<TrailPoint>> segments, TimeOptions options)
    {
        var points = segments.SelectMany(s => s).ToList();
        var issue = CheckStart(options.StartLocal, options.TimeZone);
        var metersPerSecond = ToMetersPerSecond(options.Speed, options.Unit);
        var invalidRate = options.Mode == TimeMode.ByDistance
            ? !(double.IsFinite(metersPerSecond) && metersPerSecond > 0)
            : options.Interval <= TimeSpan.Zero;
        if (points.Count == 0 || issue == TimeStartIssue.Invalid || invalidRate)
        {
            return null;
        }

        // Ambiguous local times are converted as standard time (documented, shown in the preview).
        var startUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(options.StartLocal, DateTimeKind.Unspecified), options.TimeZone), TimeSpan.Zero);
        var times = new List<(TrailPoint, DateTimeOffset)>(points.Count);
        var distance = 0.0;
        var index = 0;
        TrailPoint? previous = null;

        foreach (var segment in segments)
        {
            for (var i = 0; i < segment.Count; i++)
            {
                var point = segment[i];
                if (previous is not null && (i > 0 || options.IncludeSegmentGaps))
                {
                    distance += Geodesy.Distance(previous.Position, point.Position);
                }

                var offset = options.Mode == TimeMode.ByDistance
                    ? TimeSpan.FromSeconds(distance / metersPerSecond)
                    : options.Interval * index;
                times.Add((point, RoundToMilliseconds(startUtc + offset)));
                previous = point;
                index++;
            }
        }

        return new TimePlan(
            startUtc,
            times[^1].Item2,
            points.Count,
            points.Count(p => p.Time is not null),
            issue,
            times,
            new SetTimesCommand(times));
    }

    private static DateTimeOffset RoundToMilliseconds(DateTimeOffset time) =>
        new(time.UtcTicks - time.UtcTicks % TimeSpan.TicksPerMillisecond
            + (time.UtcTicks % TimeSpan.TicksPerMillisecond >= TimeSpan.TicksPerMillisecond / 2 ? TimeSpan.TicksPerMillisecond : 0), TimeSpan.Zero);
}
