using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Domain.Processing;
using Polaris.Gps.Domain.Profiles;

namespace Polaris.Gps.Tests.Domain;

/// <summary>Tools › Clean track: GPS jumps, spikes, stops and elevation smoothing.</summary>
public sealed class CleanTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

    /// <summary>About 11 m north per step (0.0001° of latitude), one point every 10 s: 4 km/h.</summary>
    private static List<TrailPoint> Walk(int count) =>
        Enumerable.Range(0, count).Select(i => new TrailPoint(new GeoPosition(42.8 + i * 0.0001, -1.6))
        {
            Time = Start.AddSeconds(i * 10),
            Elevation = 500 + i,
            Name = $"p{i}",
        }).ToList();

    private static CleaningOptions Only(double? speed = null, double? spike = null, double? stop = null, bool smooth = false) =>
        new(speed, spike, stop, smooth ? 100 : null);

    [Fact]
    public void A_point_reached_too_fast_is_a_jump()
    {
        var points = Walk(6);
        points[3].Position = new GeoPosition(42.81, -1.6); // ~1 km away in 10 s

        var plan = TrackCleaner.Plan([points], Only(speed: 50));
        plan.Command!.Do();

        Assert.Equal(1, plan.Jumps);
        Assert.DoesNotContain("p3", points.Select(p => p.Name));
    }

    [Fact]
    public void A_point_that_goes_out_and_comes_back_is_a_spike_even_without_times()
    {
        var points = Walk(5);
        points.ForEach(p => p.Time = null);
        points[2].Position = new GeoPosition(42.8002, -1.599); // ~80 m east of the line, neighbours 22 m apart

        var plan = TrackCleaner.Plan([points], Only(speed: 50, spike: 50));
        plan.Command!.Do();

        Assert.Equal(1, plan.Jumps);
        Assert.Equal(["p0", "p1", "p3", "p4"], points.Select(p => p.Name));
    }

    [Fact]
    public void A_stop_keeps_its_first_and_last_points()
    {
        var points = Walk(3);
        var stop = points[2].Position;
        points.AddRange(Enumerable.Range(0, 5).Select(i => new TrailPoint(new GeoPosition(stop.Latitude + i * 0.00001, stop.Longitude)) { Name = $"s{i}" }));
        points.Add(new TrailPoint(new GeoPosition(stop.Latitude + 0.001, stop.Longitude)) { Name = "end" });

        var plan = TrackCleaner.Plan([points], Only(stop: 10));
        plan.Command!.Do();

        // p2 and s0..s4 are within 10 m of p2: p2 and s4 are kept
        Assert.Equal(4, plan.StopPoints);
        Assert.Equal(["p0", "p1", "p2", "s4", "end"], points.Select(p => p.Name));
    }

    [Fact]
    public void Smoothing_uses_the_gaussian_average_of_the_profile_and_records_the_origin()
    {
        var points = Walk(40);
        points[20].Elevation = 600; // a 80 m glitch

        var plan = TrackCleaner.Plan([points], Only(smooth: true));
        var expected = ProfileStatistics.Smooth(points.Select(p => p.Elevation!.Value).ToArray(), 9); // 100 m / 11 m ≈ 9 points
        plan.Command!.Do();

        Assert.Equal(0, plan.Removed);
        Assert.Equal(Math.Round(expected[20], 1), points[20].Elevation);
        Assert.True(points[20].Elevation < 560);
        Assert.Equal(ElevationSource.Smoothed, points[20].ElevationSource);
    }

    [Fact]
    public void A_wider_window_smooths_more_and_an_invalid_one_is_refused()
    {
        var narrow = Walk(60);
        var wide = Walk(60);
        narrow[30].Elevation = wide[30].Elevation = 600;

        TrackCleaner.Plan([narrow], new CleaningOptions(null, null, null, 100)).Command!.Do();
        TrackCleaner.Plan([wide], new CleaningOptions(null, null, null, 300)).Command!.Do();

        Assert.True(wide[30].Elevation < narrow[30].Elevation);
        Assert.Equal(Math.Round(ProfileStatistics.Smooth(Walk(60).Select((p, i) => i == 30 ? 600.0 : p.Elevation!.Value).ToArray(), 27)[30], 1), wide[30].Elevation);
    }

    [Fact]
    public void The_ends_are_never_removed_and_undo_restores_everything()
    {
        var points = Walk(6);
        points[5].Position = new GeoPosition(42.9, -1.6); // a jump at the very end stays
        var original = points.Select(p => (p, p.Elevation)).ToList();
        points[2].Position = new GeoPosition(42.81, -1.6);

        var plan = TrackCleaner.Plan([points], Only(speed: 50, smooth: true));
        plan.Command!.Do();
        Assert.Equal("p5", points[^1].Name);
        Assert.Equal("p0", points[0].Name);

        plan.Command.Undo();
        Assert.Equal(original.Select(o => o.p), points);
        Assert.Equal(original.Select(o => o.Elevation), points.Select(p => p.Elevation));
    }

    [Fact]
    public void Nothing_to_clean_gives_no_command()
    {
        Assert.Null(TrackCleaner.Plan([Walk(10)], Only(speed: 50, spike: 50, stop: 5)).Command);
    }
}
