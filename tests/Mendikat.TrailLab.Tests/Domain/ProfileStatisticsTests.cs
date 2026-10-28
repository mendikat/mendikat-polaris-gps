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

    /// <summary>Points <paramref name="spacing"/> meters apart northwards with the given elevations.</summary>
    private static List<TrailPoint> Spaced(double spacing, IEnumerable<double> elevations) =>
        elevations.Select((e, i) => new TrailPoint(new GeoPosition(42 + (i * spacing / 111_000.0), -1.6)) { Elevation = e }).ToList();

    [Fact]
    public void A_climb_between_flat_stretches_is_counted_in_full()
    {
        // 200 m flat at 500 m, climb to 600 m in 200 m, 200 m flat; points every 10 m (a 10-point window).
        var elevations = Enumerable.Repeat(500.0, 20)
            .Concat(Enumerable.Range(1, 20).Select(i => 500.0 + (i * 5)))
            .Concat(Enumerable.Repeat(600.0, 20));

        var profile = ElevationProfile.Build([Spaced(10, elevations)]);

        Assert.Equal(100, profile.Ascent, 6);
        Assert.Equal(0, profile.Descent, 6);
    }

    [Fact]
    public void Gps_noise_is_smoothed_out()
    {
        // ±1.5 m oscillation every 10 m along 1 km: summing point to point would give ~150 m each way.
        var elevations = Enumerable.Range(0, 100).Select(i => i % 2 == 0 ? 500.0 : 503.0).ToList();
        var samples = ElevationProfile.Build([Spaced(10, elevations)]).Samples;

        var (ascent, descent) = ProfileStatistics.ElevationGain(samples);

        Assert.Equal((150.0, 147.0), ProfileStatistics.GainAndLoss(elevations));
        // Only the net 1.5 m from the low first value to the high last one remains (ends keep their elevation).
        Assert.Equal(1.5, ascent, 6);
        Assert.Equal(0, descent, 6);
    }

    [Theory]
    [InlineData(10, 50, 10)]   // 100 m / 10 m
    [InlineData(3, 200, 33)]   // 100 m / 3 m, rounded
    [InlineData(200, 50, 1)]   // points further apart than the window: no smoothing
    [InlineData(1, 500, 100)]  // steps of 1 m are stops: no movement, largest window (100 m / 1 m)…
    [InlineData(1, 40, 40)]    // …but never more points than there are
    public void The_window_is_100_m_whatever_the_point_spacing(double spacing, int count, int expected)
    {
        var samples = ElevationProfile.Build([Spaced(spacing, Enumerable.Repeat(500.0, count))]).Samples;

        Assert.Equal(expected, ProfileStatistics.WindowPoints(samples, count));
    }

    [Fact]
    public void Stops_do_not_shrink_the_mean_spacing()
    {
        // 10 m steps with the device left still for 30 points in the middle: the window stays at 10 points.
        var line = Spaced(10, Enumerable.Repeat(500.0, 31));
        var points = Enumerable.Range(0, 60)
            .Select(i => new TrailPoint(line[i <= 20 ? i : i < 50 ? 20 : i - 29].Position) { Elevation = 500 })
            .ToList();

        var samples = ElevationProfile.Build([points]).Samples;

        Assert.Equal(10, ProfileStatistics.WindowPoints(samples, points.Count));
    }

    [Fact]
    public void In_a_circuit_the_descent_equals_the_ascent()
    {
        // There and back along the same line: start and end coincide.
        var elevations = Enumerable.Range(0, 30).Select(i => 500.0 + (i * 2)).Concat(Enumerable.Range(0, 30).Select(i => 558.0 - (i * 1.9)));
        var points = Spaced(10, elevations);
        for (var i = 30; i < 60; i++)
        {
            points[i].Position = points[59 - i].Position;
        }

        var profile = ElevationProfile.Build([points]);

        Assert.True(profile.Ascent > 50);
        Assert.Equal(profile.Ascent, profile.Descent);
    }

    [Fact]
    public void Gives_the_same_result_as_mendikat_net()
    {
        // Values computed by GaussianElevationAnalyzer.php (mendikat.net, commit d259429a). The same comparison on
        // 573 segments of 500 real tracks (2026-10-26) gave identical results to 0.001 m.
        var points = new List<TrailPoint>
        {
            new(new GeoPosition(42, -1.6)) { Elevation = 500 },
            new(new GeoPosition(42.0001, -1.6)) { Elevation = 510 },
            new(new GeoPosition(42.0002, -1.6)) { Elevation = 505 },
        };

        var profile = ElevationProfile.Build([points]);

        Assert.Equal(7.337, profile.Ascent, 3);
        Assert.Equal(7.337, profile.Descent, 3); // 22 m from start to end: a circuit
    }

    [Fact]
    public void Segments_are_measured_separately_and_missing_elevations_are_skipped()
    {
        var track = new Track();
        var first = new TrackSegment();
        first.Points.AddRange(Points([500, null, 520]));
        var second = new TrackSegment();
        second.Points.AddRange(Points([900, 905, 910])); // the 380 m jump between segments is not counted
        track.Segments.AddRange([first, second]);

        var profile = ElevationProfile.Build(track);

        Assert.Equal(30, profile.Ascent, 6);
        Assert.Equal(0, profile.Descent, 6);
    }

    [Fact]
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

    /// <summary>Points northwards at the given distances from the start (meters) and times (seconds).</summary>
    private static List<TrailPoint> Walk(IEnumerable<(double Meters, double Seconds)> steps) =>
        steps.Select(s => new TrailPoint(new GeoPosition(42 + (s.Meters / 111_050.0), -1.6)) { Time = Start.AddSeconds(s.Seconds) }).ToList();

    [Fact]
    public void Average_speed_counts_only_the_time_in_movement()
    {
        // 100 m in 60 s, a 10-minute stop, 100 m in 60 s: 200 m in 2 moving minutes = 6 km/h.
        var times = ElevationProfile.Build([Walk([(0, 0), (100, 60), (100, 660), (200, 720)])]).Times!;

        Assert.Equal(200, times.MovingDistance, 0);
        Assert.Equal(6.0, times.AverageSpeed!.Value * 3.6, 1);
    }

    [Fact]
    public void Maximum_speed_is_the_fastest_stretch_of_at_least_30_seconds()
    {
        // Every 10 s: 2 minutes at 1 m/s, then 1 minute at 3 m/s.
        var steps = Enumerable.Range(0, 13).Select(i => ((double)i * 10, i * 10.0))
            .Concat(Enumerable.Range(1, 6).Select(i => (120 + (i * 30.0), 120 + (i * 10.0))));

        var times = ElevationProfile.Build([Walk(steps)]).Times!;

        Assert.Equal(3.0, times.MaxSpeed!.Value, 2);
    }

    [Fact]
    public void A_gps_jump_does_not_set_the_maximum_speed()
    {
        // 1 m/s every 5 s for 2 minutes, but one point jumps 45 m ahead and back: 9 m/s point to point.
        var steps = Enumerable.Range(0, 25).Select(i => (i * 5.0 + (i == 12 ? 45 : 0), i * 5.0)).ToList();
        var samples = ElevationProfile.Build([Walk(steps)]).Samples;
        var pointToPoint = samples.Zip(samples.Skip(1), (a, b) => (b.Distance - a.Distance) / 5).Max();

        var max = ProfileStatistics.Times(samples)!.MaxSpeed!.Value;

        Assert.True(pointToPoint > 8, $"point to point {pointToPoint}");
        Assert.True(max < 4.1, $"max {max}");
    }

    [Fact]
    public void Without_30_seconds_of_data_there_is_no_maximum_speed()
    {
        var times = ElevationProfile.Build([Walk([(0, 0), (20, 10), (40, 20)])]).Times!;

        Assert.Null(times.MaxSpeed);
        Assert.NotNull(times.AverageSpeed);
    }

    [Fact]
    public void Missing_or_single_times_give_no_time_statistics()
    {
        Assert.Null(ElevationProfile.Build([Points([500, 510])]).Times);
        Assert.Null(ElevationProfile.Build([Points([500], [0])]).Times);
        Assert.Equal(0, ElevationProfile.Build([]).Ascent);
    }

    [Theory]
    [InlineData(7500, "2 h 05 min")]
    [InlineData(750, "12 min 30 s")]
    [InlineData(0, "0 min 00 s")]
    public void Durations_are_shown_in_hours_or_minutes(int seconds, string expected)
    {
        Assert.Equal(expected, ProfileViewModel.Duration(TimeSpan.FromSeconds(seconds)));
    }
}
