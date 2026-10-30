using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;

namespace Polaris.Gps.Domain.Profiles;

/// <summary>A point of the profile: cumulative horizontal distance and elevation (null if the point has none).</summary>
/// <param name="Distance">Cumulative geodesic distance in meters from the first point (PROF-02).</param>
/// <param name="Segment">0-based segment index.</param>
/// <param name="Index">0-based position of the point in its segment.</param>
public sealed record ProfileSample(TrailPoint Point, double Distance, double? Elevation, int Segment, int Index);

/// <summary>
/// Elevation profile of a track or route (PROF-01..05). Distances are horizontal geodesic distances on WGS84
/// (Karney); elevation is not used to measure. By default the gap between segments is not counted, so the next
/// segment starts at the distance where the previous one ended.
/// </summary>
public sealed class ElevationProfile
{
    private readonly Dictionary<TrailPoint, ProfileSample> _byPoint;

    private ElevationProfile(List<ProfileSample> samples)
    {
        Samples = samples;
        _byPoint = new Dictionary<TrailPoint, ProfileSample>(ReferenceEqualityComparer.Instance);
        foreach (var sample in samples)
        {
            _byPoint.TryAdd(sample.Point, sample);
        }

        TotalDistance = samples.Count > 0 ? samples[^1].Distance : 0;
        var elevations = samples.Where(s => s.Elevation is not null).Select(s => s.Elevation!.Value).ToList();
        MissingElevations = samples.Count - elevations.Count;
        MinElevation = elevations.Count > 0 ? elevations.Min() : null;
        MaxElevation = elevations.Count > 0 ? elevations.Max() : null;
        (Ascent, Descent) = ProfileStatistics.ElevationGain(samples);
        Times = ProfileStatistics.Times(samples);
    }

    /// <summary>Total ascent in meters (PROF-06), from the Gaussian-smoothed profile (<see cref="ProfileStatistics.ElevationGain"/>); 0 without elevations.</summary>
    public double Ascent { get; }

    /// <summary>Total descent in meters, positive (PROF-06).</summary>
    public double Descent { get; }

    /// <summary>Total, moving and stopped time (PROF-07), or null if fewer than two points have a time.</summary>
    public ProfileTimes? Times { get; }

    public IReadOnlyList<ProfileSample> Samples { get; }

    /// <summary>Total distance in meters.</summary>
    public double TotalDistance { get; }

    /// <summary>Number of points without elevation (PROF-03).</summary>
    public int MissingElevations { get; }

    public double? MinElevation { get; }

    public double? MaxElevation { get; }

    public bool HasElevation => MinElevation is not null;

    private ProfileAnalysis? _analysis;

    /// <summary>Slope and speed of each sample and the climbs, computed when first needed.</summary>
    public ProfileAnalysis Analysis => _analysis ??= ProfileAnalysis.Analyze(Samples);

    /// <param name="segments">Ordered point lists (track segments, or a single route list).</param>
    /// <param name="includeSegmentGaps">If true, the geodesic distance between consecutive segments is added.</param>
    public static ElevationProfile Build(IEnumerable<IReadOnlyList<TrailPoint>> segments, bool includeSegmentGaps = false)
    {
        var samples = new List<ProfileSample>();
        var distance = 0.0;
        TrailPoint? previous = null;
        var segmentIndex = 0;

        foreach (var segment in segments)
        {
            for (var i = 0; i < segment.Count; i++)
            {
                var point = segment[i];
                if (previous is not null && (i > 0 || includeSegmentGaps))
                {
                    distance += Geodesy.Distance(previous.Position, point.Position);
                }

                samples.Add(new ProfileSample(point, distance, point.Elevation, segmentIndex, i));
                previous = point;
            }

            segmentIndex++;
        }

        return new ElevationProfile(samples);
    }

    public static ElevationProfile Build(Track track, bool includeSegmentGaps = false) =>
        Build(track.Segments.Select(s => (IReadOnlyList<TrailPoint>)s.Points), includeSegmentGaps);

    public static ElevationProfile Build(Route route) => Build([route.Points]);

    /// <summary>The sample of <paramref name="point"/>, or null if it is not in the profile.</summary>
    public ProfileSample? Find(TrailPoint point) => _byPoint.GetValueOrDefault(point);

    /// <summary>The sample nearest to <paramref name="distance"/> meters (the first one on ties), or null if there are none.</summary>
    public ProfileSample? Nearest(double distance)
    {
        if (Samples.Count == 0)
        {
            return null;
        }

        // Binary search for the first sample at or after the distance (distances never decrease).
        int low = 0, high = Samples.Count - 1;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (Samples[mid].Distance < distance)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        if (low > 0 && distance - Samples[low - 1].Distance <= Samples[low].Distance - distance)
        {
            low--;
            while (low > 0 && Samples[low - 1].Distance == Samples[low].Distance)
            {
                low--;
            }
        }

        return Samples[low];
    }

    /// <summary>
    /// Runs of consecutive samples that have elevation, split at missing elevations and at segment boundaries
    /// (PROF-03): the curve is drawn as separate lines, never joining across a gap.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<ProfileSample>> ElevationRuns()
    {
        var runs = new List<IReadOnlyList<ProfileSample>>();
        List<ProfileSample>? current = null;
        foreach (var sample in Samples)
        {
            if (sample.Elevation is null || (current is not null && current[^1].Segment != sample.Segment))
            {
                Close();
            }

            if (sample.Elevation is not null)
            {
                (current ??= []).Add(sample);
            }
        }

        Close();
        return runs;

        void Close()
        {
            if (current is { Count: > 0 })
            {
                runs.Add(current);
            }

            current = null;
        }
    }
}
