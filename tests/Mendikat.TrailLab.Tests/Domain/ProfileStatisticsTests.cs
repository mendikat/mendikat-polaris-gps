using Mendikat.TrailLab.App.ViewModels;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Domain.Model;
using Mendikat.TrailLab.Domain.Profiles;

namespace Mendikat.TrailLab.Tests.Domain;

public sealed class ProfileStatisticsTests
{
    private static readonly DateTimeOffset Start = new(2026, 5, 1, 7, 0, 0, TimeSpan.Zero);

    /// <summary>Points 100 m apart northwards with the given elevations and, optionally, seconds from the start.</summary>
    private static List<TrailPoint> Points(double?[] elevations, int[]? seconds = null) =>
        elevations.Select((e, i) => new TrailPoint(new GeoPosition(42 + i * 0.0009, -1.6))
        {
            Elevation = e,
            Time = seconds is null ? null : Start.AddSeconds(seconds[i]),
        }).ToList();

    [Fact]
    [Trait("Req", "PROF-06")]
    public void Ascent_and_descent_add_real_climbs_and_ignore_small_noise()
    {
        // Up 100 m with ±2 m GPS noise, then down 60 m.
        var profile = ElevationProfile.Build([Points([500, 502, 500, 520, 518, 560, 600, 598, 600, 570, 540])]);

        Assert.Equal(100, profile.Ascent, 6);
        Assert.Equal(60, profile.Descent, 6);
    }

    [Fact]
    [Trait("Req", "PROF-06")]
    public void Without_a_filter_noise_would_inflate_the_gain()
    {
        var samples = ElevationProfile.Build([Points([500, 503, 500, 503, 500, 503, 500])]).Samples;

        Assert.Equal((0.0, 0.0), ProfileStatistics.ElevationGain(samples));        // 5 m filter
        Assert.Equal((9.0, 9.0), ProfileStatistics.ElevationGain(samples, 0)); // raw sum
    }

    [Fact]
    [Trait("Req", "PROF-06")]
    public void Segments_are_measured_separately_and_missing_elevations_are_skipped()
    {
        var track = new Track();
        var first = new TrackSegment();
        first.Points.AddRange(Points([500, null, 520]));
        var second = new TrackSegment();
        second.Points.AddRange(Points([900, 910])); // the 380 m jump between segments is not counted
        track.Segments.AddRange([first, second]);

        var profile = ElevationProfile.Build(track);

        Assert.Equal(30, profile.Ascent, 6);
        Assert.Equal(0, profile.Descent, 6);
    }

    [Fact]
    [Trait("Req", "PROF-07")]
    public void Slow_intervals_count_as_stopped_time()
    {
        // 100 m in 60 s (6 km/h, moving), then the same place for 10 min (stopped), then 100 m in 60 s.
        var points = Points([500, 510, 510, 520], [0, 60, 660, 720]);
        points[2].Position = points[1].Position;
        points[3].Position = new GeoPosition(points[1].Position.Latitude + 0.0009, -1.6);

        var times = ElevationProfile.Build([points]).Times!;

        Assert.Equal(TimeSpan.FromMinutes(12), times.Total);
        Assert.Equal(TimeSpan.FromMinutes(2), times.Moving);
        Assert.Equal(TimeSpan.FromMinutes(10), times.Stopped);
    }

    [Fact]
    [Trait("Req", "PROF-07")]
    public void The_pause_between_segments_is_stopped_time()
    {
        var track = new Track();
        var first = new TrackSegment();
        first.Points.AddRange(Points([500, 510], [0, 60]));
        var second = new TrackSegment();
        second.Points.AddRange(Points([510, 520], [1860, 1920])); // 30 min later
        track.Segments.AddRange([first, second]);

        var times = ElevationProfile.Build(track).Times!;

        Assert.Equal(TimeSpan.FromMinutes(32), times.Total);
        Assert.Equal(TimeSpan.FromMinutes(2), times.Moving);
        Assert.Equal(TimeSpan.FromMinutes(30), times.Stopped);
    }

    [Fact]
    [Trait("Req", "PROF-07")]
    [Trait("Req", "PROF-05")]
    public void Missing_or_single_times_give_no_time_statistics()
    {
        Assert.Null(ElevationProfile.Build([Points([500, 510])]).Times);
        Assert.Null(ElevationProfile.Build([Points([500], [0])]).Times);
        Assert.Equal(0, ElevationProfile.Build([]).Ascent);
    }

    [Theory]
    [Trait("Req", "PROF-07")]
    [InlineData(7500, "2 h 05 min")]
    [InlineData(750, "12 min 30 s")]
    [InlineData(0, "0 min 00 s")]
    public void Durations_are_shown_in_hours_or_minutes(int seconds, string expected)
    {
        Assert.Equal(expected, ProfileViewModel.Duration(TimeSpan.FromSeconds(seconds)));
    }
}
