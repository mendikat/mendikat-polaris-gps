using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Domain.Profiles;

namespace Polaris.Gps.Tests.Domain;

public sealed class ElevationProfileTests
{
    private const double OneDegreeAtEquator = 111_319.490793;

    private static TrailPoint P(double lon, double? elevation = 100) => new(new GeoPosition(0, lon)) { Elevation = elevation };

    [Fact]
    public void Distance_is_cumulative_geodesic_and_horizontal()
    {
        var profile = ElevationProfile.Build([[P(0, 100), P(1, 2000), P(2, 100)]]);

        Assert.Equal([0, OneDegreeAtEquator, 2 * OneDegreeAtEquator], profile.Samples.Select(s => s.Distance), new ToleranceComparer(0.001));
        Assert.Equal(2 * OneDegreeAtEquator, profile.TotalDistance, 0.001); // elevation does not change the distance
        Assert.Equal((100, 2000), (profile.MinElevation!.Value, profile.MaxElevation!.Value));
    }

    [Fact]
    public void The_gap_between_segments_is_not_counted_unless_asked()
    {
        var track = new Track();
        track.Segments.Add(Segment(P(0), P(1)));
        track.Segments.Add(Segment(P(5), P(6)));

        var withoutGap = ElevationProfile.Build(track);
        var withGap = ElevationProfile.Build(track, includeSegmentGaps: true);

        Assert.Equal(2 * OneDegreeAtEquator, withoutGap.TotalDistance, 0.01);
        Assert.Equal(6 * OneDegreeAtEquator, withGap.TotalDistance, 0.01);
        Assert.Equal(withoutGap.Samples[1].Distance, withoutGap.Samples[2].Distance); // segment 2 starts where 1 ended
        Assert.Equal([0, 0, 1, 1], withoutGap.Samples.Select(s => s.Segment));
    }

    [Fact]
    public void Missing_elevations_are_counted_and_split_the_curve()
    {
        var track = new Track();
        track.Segments.Add(Segment(P(0, 100), P(1, null), P(2, 120), P(3, 130)));
        track.Segments.Add(Segment(P(4, 140), P(5, 150)));

        var profile = ElevationProfile.Build(track);
        var runs = profile.ElevationRuns();

        Assert.Equal(1, profile.MissingElevations);
        Assert.Equal([1, 2, 2], runs.Select(r => r.Count)); // split at the missing point and at the segment boundary
    }

    [Fact]
    public void A_track_without_elevations_has_no_curve()
    {
        var profile = ElevationProfile.Build([[P(0, null), P(1, null)]]);

        Assert.False(profile.HasElevation);
        Assert.Equal(2, profile.MissingElevations);
        Assert.Empty(profile.ElevationRuns());
        Assert.Null(profile.MinElevation);
    }

    [Fact]
    public void Empty_single_and_duplicated_points_do_not_fail()
    {
        var empty = ElevationProfile.Build([]);
        Assert.Empty(empty.Samples);
        Assert.Null(empty.Nearest(10));
        Assert.Equal(0, empty.TotalDistance);

        var single = ElevationProfile.Build([[P(0)]]);
        Assert.Same(single.Samples[0], single.Nearest(500));

        var a = P(0);
        var duplicated = ElevationProfile.Build([[a, P(0), P(0), P(1)]]);
        Assert.Equal([0, 0, 0], duplicated.Samples.Take(3).Select(s => s.Distance));
        Assert.Same(a, duplicated.Nearest(0)!.Point); // the first of the duplicates
    }

    [Fact]
    public void Nearest_and_find_map_between_distances_and_points()
    {
        var points = new[] { P(0), P(1), P(2), P(3) };
        var profile = ElevationProfile.Build([points]);

        Assert.Same(points[1], profile.Nearest(OneDegreeAtEquator * 1.4)!.Point);
        Assert.Same(points[2], profile.Nearest(OneDegreeAtEquator * 1.6)!.Point);
        Assert.Same(points[0], profile.Nearest(-50)!.Point);
        Assert.Same(points[3], profile.Nearest(1e9)!.Point);
        Assert.Equal(2 * OneDegreeAtEquator, profile.Find(points[2])!.Distance, 0.001);
        Assert.Null(profile.Find(P(9)));
    }

    [Fact]
    public void Profile_of_100000_points_builds_quickly()
    {
        var points = Enumerable.Range(0, 100_000).Select(i => new TrailPoint(new GeoPosition(42 + i * 1e-5, -1.6)) { Elevation = i % 300 }).ToList();
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var profile = ElevationProfile.Build([points]);

        Assert.Equal(100_000, profile.Samples.Count);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"took {watch.Elapsed}");
    }

    private static TrackSegment Segment(params TrailPoint[] points)
    {
        var segment = new TrackSegment();
        segment.Points.AddRange(points);
        return segment;
    }

    private sealed class ToleranceComparer(double tolerance) : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) <= tolerance;

        public int GetHashCode(double obj) => 0;
    }
}
