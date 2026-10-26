namespace Mendikat.TrailLab.Domain.Profiles;

/// <param name="Total">From the first to the last timed point.</param>
/// <param name="Moving">Sum of the intervals inside segments where the horizontal speed reaches <see cref="ProfileStatistics.StoppedSpeed"/>.</param>
/// <param name="Stopped">Total − moving: slow intervals, pauses between segments and intervals without usable times.</param>
/// <param name="MovingDistance">Meters covered in the moving intervals.</param>
/// <param name="MaxSpeed">Highest speed (m/s) kept for at least <see cref="ProfileStatistics.MaxSpeedWindow"/>, or null if no stretch
/// lasts that long.</param>
public sealed record ProfileTimes(TimeSpan Total, TimeSpan Moving, TimeSpan Stopped, double MovingDistance = 0, double? MaxSpeed = null)
{
    /// <summary>Average moving speed in m/s: moving distance ÷ moving time; null without movement.</summary>
    public double? AverageSpeed => Moving > TimeSpan.Zero ? MovingDistance / Moving.TotalSeconds : null;
}

/// <summary>
/// Ascent/descent and time statistics of a profile (PROF-06, PROF-07). GPS elevations and positions are noisy, so both
/// use documented filters (ADR-0008):
/// <list type="bullet">
/// <item>Elevation gain from a profile smoothed with a centred Gaussian moving average (ALMA), the method used by
/// mendikat.net since version 18.6 (<c>GaussianElevationAnalyzer.php</c>). The window is <see cref="SmoothingWindow"/>
/// meters, so the result does not depend on how often the device recorded points. Each segment is measured on its own
/// (the jump between segments is not counted); missing elevations are skipped; in a circuit the descent is set equal
/// to the ascent.</item>
/// <item>An interval between consecutive points of the same segment is "moving" if the horizontal speed is at least
/// <see cref="StoppedSpeed"/>; the time between segments is a pause (stopped).</item>
/// </list>
/// </summary>
public static class ProfileStatistics
{
    /// <summary>Width of the smoothing window, in meters along the track.</summary>
    public const double SmoothingWindow = 100;

    /// <summary>Width factor σ of the Gaussian: the bell has a standard deviation of window / σ points.</summary>
    public const double GaussianSigma = 6;

    /// <summary>Steps of this length or less (meters) are stops and do not count for the mean point spacing.</summary>
    public const double MinimumStep = 1;

    /// <summary>A segment whose first and last points are closer than this (meters) is a circuit.</summary>
    public const double CircuitDistance = 100;

    /// <summary>Below this horizontal speed (1 km/h, in m/s) an interval counts as stopped.</summary>
    public const double StoppedSpeed = 1 / 3.6;

    /// <summary>
    /// The maximum speed is the highest average over any stretch of at least this duration. Point-to-point speeds
    /// are dominated by GPS jumps (a 10 m error in 1 s reads as 36 km/h).
    /// </summary>
    public static readonly TimeSpan MaxSpeedWindow = TimeSpan.FromSeconds(30);

    public static (double Ascent, double Descent) ElevationGain(IReadOnlyList<ProfileSample> samples)
    {
        double ascent = 0, descent = 0;
        foreach (var segment in samples.GroupBy(s => s.Segment))
        {
            var points = segment.ToList();
            var elevations = points.Where(s => s.Elevation is not null).Select(s => s.Elevation!.Value).ToArray();
            if (elevations.Length < 2)
            {
                continue;
            }

            var (gain, loss) = GainAndLoss(Smooth(elevations, WindowPoints(points, elevations.Length)));
            if (IsCircuit(points))
            {
                loss = gain;
            }

            ascent += gain;
            descent += loss;
        }

        return (ascent, descent);
    }

    /// <summary>
    /// Number of points of the smoothing window: <see cref="SmoothingWindow"/> divided by the mean spacing of the steps
    /// longer than <see cref="MinimumStep"/> (stops would shrink the spacing and widen the real window), between 1 and
    /// <paramref name="count"/>. Points further apart than the window are not smoothed (1); with no movement at all
    /// the largest window is used, since elevation then only changes by noise.
    /// </summary>
    /// <param name="segment">The samples of one segment, in order.</param>
    /// <param name="count">Number of elevations to smooth.</param>
    public static int WindowPoints(IReadOnlyList<ProfileSample> segment, int count)
    {
        var length = 0.0;
        var steps = 0;
        for (var i = 1; i < segment.Count; i++)
        {
            var step = segment[i].Distance - segment[i - 1].Distance;
            if (step > MinimumStep)
            {
                length += step;
                steps++;
            }
        }

        if (steps == 0)
        {
            return Math.Min((int)(SmoothingWindow / MinimumStep), count);
        }

        var points = (int)Math.Round(SmoothingWindow / (length / steps), MidpointRounding.AwayFromZero);
        return Math.Min(Math.Max(points, 1), count);
    }

    /// <summary>
    /// Centred Gaussian moving average (ALMA with offset 0.5) over <paramref name="window"/> points. Weights are
    /// e^(−(j − m)² / 2s²) with m = (k − 1) / 2 and s = k / σ. The series is extended by repeating its first and last
    /// values, so the result has as many values as the input and the ends keep their elevation.
    /// </summary>
    public static double[] Smooth(IReadOnlyList<double> elevations, int window)
    {
        var count = elevations.Count;
        var before = (window - 1) / 2;
        var centre = 0.5 * (window - 1);
        var width = window / GaussianSigma;
        var weights = new double[window];
        for (var j = 0; j < window; j++)
        {
            weights[j] = Math.Exp(-((j - centre) * (j - centre)) / (2 * width * width));
        }

        var weightSum = weights.Sum();
        var smoothed = new double[count];
        for (var t = 0; t < count; t++)
        {
            var sum = 0.0;
            for (var j = 0; j < window; j++)
            {
                sum += elevations[Math.Clamp(t + j - before, 0, count - 1)] * weights[j];
            }

            smoothed[t] = sum / weightSum;
        }

        return smoothed;
    }

    /// <summary>Sum of the rises and of the falls between consecutive values (both positive).</summary>
    public static (double Gain, double Loss) GainAndLoss(IReadOnlyList<double> elevations)
    {
        double gain = 0, loss = 0;
        for (var i = 1; i < elevations.Count; i++)
        {
            var change = elevations[i] - elevations[i - 1];
            if (change >= 0)
            {
                gain += change;
            }
            else
            {
                loss -= change;
            }
        }

        return (gain, loss);
    }

    /// <summary>The segment ends less than <see cref="CircuitDistance"/> from where it starts.</summary>
    private static bool IsCircuit(IReadOnlyList<ProfileSample> segment) =>
        Geo.Geodesy.Distance(segment[0].Point.Position, segment[^1].Point.Position) < CircuitDistance;

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
        var movingDistance = 0.0;
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
                movingDistance += current.Distance - previous.Distance;
            }
        }

        moving = moving > total ? total : moving;
        return new ProfileTimes(total, moving, total - moving, movingDistance, MaxSpeed(timed));
    }

    /// <summary>
    /// Highest average speed over a stretch of at least <see cref="MaxSpeedWindow"/> inside one segment (two pointers:
    /// for each start, the first point at least that much later). Null if no stretch lasts long enough.
    /// </summary>
    /// <param name="timed">Samples with a time, in order.</param>
    public static double? MaxSpeed(IReadOnlyList<ProfileSample> timed)
    {
        double? max = null;
        var end = 0;
        for (var start = 0; start < timed.Count; start++)
        {
            end = Math.Max(end, start + 1);
            var startTime = timed[start].Point.Time!.Value;
            while (end < timed.Count && timed[end].Segment == timed[start].Segment && timed[end].Point.Time!.Value - startTime < MaxSpeedWindow)
            {
                end++;
            }

            if (end >= timed.Count || timed[end].Segment != timed[start].Segment)
            {
                continue; // no stretch from this point lasts long enough within its segment
            }

            var speed = (timed[end].Distance - timed[start].Distance) / (timed[end].Point.Time!.Value - startTime).TotalSeconds;
            max = max is { } m ? Math.Max(m, speed) : speed;
        }

        return max;
    }
}
