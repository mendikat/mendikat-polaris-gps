namespace Mendikat.TrailLab.Domain.Profiles;

/// <param name="Total">From the first to the last timed point.</param>
/// <param name="Moving">Sum of the intervals inside segments where the horizontal speed reaches <see cref="ProfileStatistics.StoppedSpeed"/>.</param>
/// <param name="Stopped">Total − moving: slow intervals, pauses between segments and intervals without usable times.</param>
public sealed record ProfileTimes(TimeSpan Total, TimeSpan Moving, TimeSpan Stopped);

/// <summary>
/// Ascent/descent and time statistics of a profile (PROF-06, PROF-07). GPS elevations and positions are noisy, so both
/// use documented filters (ADR-0008):
/// <list type="bullet">
/// <item>Elevation gain with hysteresis: a change is counted only once the elevation has moved at least
/// <see cref="ElevationHysteresis"/> meters from the last counted level. Missing elevations are skipped; segments are
/// measured separately (the jump between segments is not counted).</item>
/// <item>An interval between consecutive points of the same segment is "moving" if the horizontal speed is at least
/// <see cref="StoppedSpeed"/>; the time between segments is a pause (stopped).</item>
/// </list>
/// </summary>
public static class ProfileStatistics
{
    /// <summary>Minimum elevation change, in meters, before it is counted as ascent or descent.</summary>
    public const double ElevationHysteresis = 5;

    /// <summary>Below this horizontal speed (1 km/h, in m/s) an interval counts as stopped.</summary>
    public const double StoppedSpeed = 1 / 3.6;

    public static (double Ascent, double Descent) ElevationGain(IReadOnlyList<ProfileSample> samples, double hysteresis = ElevationHysteresis)
    {
        double ascent = 0, descent = 0;
        double? reference = null;
        var segment = -1;
        foreach (var sample in samples)
        {
            if (sample.Segment != segment)
            {
                segment = sample.Segment;
                reference = null; // a new segment starts a new count
            }

            if (sample.Elevation is not { } elevation)
            {
                continue;
            }

            if (reference is not { } level)
            {
                reference = elevation;
            }
            else if (elevation - level >= hysteresis)
            {
                ascent += elevation - level;
                reference = elevation;
            }
            else if (level - elevation >= hysteresis)
            {
                descent += level - elevation;
                reference = elevation;
            }
        }

        return (ascent, descent);
    }

    public static ProfileTimes? Times(IReadOnlyList<ProfileSample> samples, double stoppedSpeed = StoppedSpeed)
    {
        var timed = samples.Where(s => s.Point.Time is not null).ToList();
        if (timed.Count < 2)
        {
            return null;
        }

        var total = timed[^1].Point.Time!.Value - timed[0].Point.Time!.Value;
        if (total <= TimeSpan.Zero)
        {
            return new ProfileTimes(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
        }

        var moving = TimeSpan.Zero;
        for (var i = 1; i < timed.Count; i++)
        {
            var (previous, current) = (timed[i - 1], timed[i]);
            var interval = current.Point.Time!.Value - previous.Point.Time!.Value;
            if (interval <= TimeSpan.Zero || current.Segment != previous.Segment)
            {
                continue; // unordered times or a pause between segments
            }

            var speed = (current.Distance - previous.Distance) / interval.TotalSeconds;
            if (speed >= stoppedSpeed)
            {
                moving += interval;
            }
        }

        moving = moving > total ? total : moving;
        return new ProfileTimes(total, moving, total - moving);
    }
}
