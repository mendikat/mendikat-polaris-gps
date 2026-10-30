namespace Polaris.Gps.Domain.Profiles;

/// <summary>A climb of the profile: from a low point to the next high point, with at least <see cref="ProfileAnalysis.MinimumClimb"/> of ascent.</summary>
/// <param name="StartDistance">Meters from the start of the profile.</param>
/// <param name="Length">Horizontal length in meters.</param>
/// <param name="Gain">Elevation gained, in meters (smoothed profile).</param>
/// <param name="StartElevation">Smoothed elevation at the start, in meters.</param>
/// <param name="MaxSlope">Steepest slope inside the climb, in percent (see <see cref="ProfileAnalysis.SlopeBase"/>).</param>
public sealed record Climb(double StartDistance, double Length, double Gain, double StartElevation, double MaxSlope)
{
    /// <summary>Average slope in percent.</summary>
    public double AverageSlope => Length > 0 ? Gain / Length * 100 : 0;

    public double EndDistance => StartDistance + Length;
}

/// <summary>
/// Values derived from a profile, one per sample: slope and speed, and the climbs. Slopes and climbs use the elevation
/// smoothed as the ascent is (<see cref="ProfileStatistics.Smooth"/>, 100 m Gaussian of mendikat.net), so GPS noise does
/// not show as steep ramps. Each segment is measured on its own.
/// </summary>
public sealed class ProfileAnalysis
{
    /// <summary>The slope of a point is measured over this horizontal distance centred on it (meters).</summary>
    public const double SlopeBase = 50;

    /// <summary>The speed of a point is the average over this time centred on it.</summary>
    public static readonly TimeSpan SpeedWindow = TimeSpan.FromSeconds(30);

    /// <summary>Climbs with less ascent than this (meters) are not listed.</summary>
    public const double MinimumClimb = 50;

    /// <summary>A climb ends when the smoothed elevation falls this much (meters) below its highest point.</summary>
    public const double ClimbEndDrop = 20;

    private ProfileAnalysis(double?[] smoothed, double?[] slopes, double?[] speeds, IReadOnlyList<Climb> climbs)
    {
        SmoothedElevations = smoothed;
        Slopes = slopes;
        Speeds = speeds;
        Climbs = climbs;
    }

    /// <summary>Smoothed elevation of each sample (meters), null where the point has no elevation.</summary>
    public IReadOnlyList<double?> SmoothedElevations { get; }

    /// <summary>Slope of each sample in percent (positive uphill), null without elevation or on a too short segment.</summary>
    public IReadOnlyList<double?> Slopes { get; }

    /// <summary>Speed of each sample in km/h, null without times.</summary>
    public IReadOnlyList<double?> Speeds { get; }

    public IReadOnlyList<Climb> Climbs { get; }

    public bool HasSpeeds => Speeds.Any(s => s is not null);

    public static ProfileAnalysis Analyze(IReadOnlyList<ProfileSample> samples)
    {
        var smoothed = new double?[samples.Count];
        var slopes = new double?[samples.Count];
        var speeds = new double?[samples.Count];
        var climbs = new List<Climb>();
        var start = 0;
        while (start < samples.Count)
        {
            var end = start;
            while (end + 1 < samples.Count && samples[end + 1].Segment == samples[start].Segment)
            {
                end++;
            }

            AnalyzeSegment(samples, start, end, smoothed, slopes, speeds, climbs);
            start = end + 1;
        }

        return new ProfileAnalysis(smoothed, slopes, speeds, climbs);
    }

    private static void AnalyzeSegment(
        IReadOnlyList<ProfileSample> samples, int first, int last, double?[] smoothed, double?[] slopes, double?[] speeds, List<Climb> climbs)
    {
        // Smoothed elevation of the points that have one, as for the ascent.
        var withElevation = Enumerable.Range(first, last - first + 1).Where(i => samples[i].Elevation is not null).ToList();
        if (withElevation.Count >= 2)
        {
            var distances = Enumerable.Range(first, last - first + 1).Select(i => samples[i].Distance).ToList();
            var values = withElevation.Select(i => samples[i].Elevation!.Value).ToArray();
            var smooth = ProfileStatistics.Smooth(values, ProfileStatistics.WindowPoints(distances, values.Length));
            for (var k = 0; k < withElevation.Count; k++)
            {
                smoothed[withElevation[k]] = smooth[k];
            }

            ComputeSlopes(samples, withElevation, smoothed, slopes);
            FindClimbs(samples, withElevation, smoothed, slopes, climbs);
        }

        ComputeSpeeds(samples, first, last, speeds);
    }

    /// <summary>Slope at each point: rise over run between the points about <see cref="SlopeBase"/> / 2 before and after it.</summary>
    private static void ComputeSlopes(IReadOnlyList<ProfileSample> samples, List<int> indexes, double?[] smoothed, double?[] slopes)
    {
        int back = 0, ahead = 0;
        for (var k = 0; k < indexes.Count; k++)
        {
            var distance = samples[indexes[k]].Distance;
            while (back < k && samples[indexes[back + 1]].Distance <= distance - SlopeBase / 2)
            {
                back++;
            }

            ahead = Math.Max(ahead, k);
            while (ahead < indexes.Count - 1 && samples[indexes[ahead]].Distance < distance + SlopeBase / 2)
            {
                ahead++;
            }

            var run = samples[indexes[ahead]].Distance - samples[indexes[back]].Distance;
            if (run >= SlopeBase / 4)
            {
                slopes[indexes[k]] = (smoothed[indexes[ahead]]!.Value - smoothed[indexes[back]]!.Value) / run * 100;
            }
        }
    }

    /// <summary>Average speed over <see cref="SpeedWindow"/> centred on each timed point.</summary>
    private static void ComputeSpeeds(IReadOnlyList<ProfileSample> samples, int first, int last, double?[] speeds)
    {
        var timed = Enumerable.Range(first, last - first + 1).Where(i => samples[i].Point.Time is not null).ToList();
        if (timed.Count < 2)
        {
            return;
        }

        var half = SpeedWindow / 2;
        int back = 0, ahead = 0;
        for (var k = 0; k < timed.Count; k++)
        {
            var time = samples[timed[k]].Point.Time!.Value;
            while (back < k && samples[timed[back + 1]].Point.Time!.Value <= time - half)
            {
                back++;
            }

            ahead = Math.Max(ahead, k);
            while (ahead < timed.Count - 1 && samples[timed[ahead]].Point.Time!.Value < time + half)
            {
                ahead++;
            }

            var seconds = (samples[timed[ahead]].Point.Time!.Value - samples[timed[back]].Point.Time!.Value).TotalSeconds;
            if (seconds > 0)
            {
                speeds[timed[k]] = (samples[timed[ahead]].Distance - samples[timed[back]].Distance) / seconds * 3.6;
            }
        }
    }

    /// <summary>
    /// Climbs on the smoothed elevation: from the lowest point before a rise to the highest point reached before the
    /// elevation drops <see cref="ClimbEndDrop"/> below it; kept when they gain at least <see cref="MinimumClimb"/>.
    /// </summary>
    private static void FindClimbs(IReadOnlyList<ProfileSample> samples, List<int> indexes, double?[] smoothed, double?[] slopes, List<Climb> climbs)
    {
        var low = 0;
        var high = 0;
        var climbing = false;
        for (var k = 1; k < indexes.Count; k++)
        {
            var value = smoothed[indexes[k]]!.Value;
            if (!climbing)
            {
                if (value < smoothed[indexes[low]]!.Value)
                {
                    low = k;
                }
                else if (value - smoothed[indexes[low]]!.Value >= ClimbEndDrop)
                {
                    climbing = true;
                    high = k;
                }

                continue;
            }

            if (value > smoothed[indexes[high]]!.Value)
            {
                high = k;
            }
            else if (smoothed[indexes[high]]!.Value - value >= ClimbEndDrop)
            {
                Add(low, high);
                climbing = false;
                low = k;
                for (var j = high; j <= k; j++)
                {
                    if (smoothed[indexes[j]]!.Value < smoothed[indexes[low]]!.Value)
                    {
                        low = j;
                    }
                }
            }
        }

        if (climbing)
        {
            Add(low, high);
        }

        void Add(int from, int to)
        {
            var gain = smoothed[indexes[to]]!.Value - smoothed[indexes[from]]!.Value;
            if (gain < MinimumClimb)
            {
                return;
            }

            var maxSlope = Enumerable.Range(from, to - from + 1).Select(j => slopes[indexes[j]] ?? 0).Max();
            var start = samples[indexes[from]].Distance;
            climbs.Add(new Climb(start, samples[indexes[to]].Distance - start, gain, smoothed[indexes[from]]!.Value, maxSlope));
        }
    }
}
