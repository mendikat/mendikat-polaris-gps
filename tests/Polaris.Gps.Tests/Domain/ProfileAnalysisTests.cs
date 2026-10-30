using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Domain.Profiles;
using Polaris.Gps.Mapping.Layers;

namespace Polaris.Gps.Tests.Domain;

/// <summary>Slope, speed and climbs of a profile, and the track colours on the map.</summary>
public sealed class ProfileAnalysisTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

    /// <summary>Points every ~11.1 m north and every 10 s, with the given elevation function of the index.</summary>
    private static Track Track(int count, Func<int, double> elevation, bool times = true)
    {
        var segment = new TrackSegment();
        segment.Points.AddRange(Enumerable.Range(0, count).Select(i => new TrailPoint(new GeoPosition(42.8 + i * 0.0001, -1.6))
        {
            Elevation = elevation(i),
            Time = times ? Start.AddSeconds(i * 10) : null,
        }));
        var track = new Track();
        track.Segments.Add(segment);
        return track;
    }

    [Fact]
    public void A_steady_ramp_has_its_slope_and_the_speed_of_the_walk()
    {
        // +1.11 m per 11.1 m step: 10 %
        var profile = ElevationProfile.Build(Track(200, i => 1000 + i * 1.11));
        var analysis = profile.Analysis;

        Assert.InRange(analysis.Slopes[100]!.Value, 9.5, 10.5);
        Assert.InRange(analysis.Speeds[100]!.Value, 3.9, 4.1); // 11.1 m in 10 s
        var climb = Assert.Single(analysis.Climbs);
        Assert.InRange(climb.Gain, 200, 222);
        Assert.InRange(climb.AverageSlope, 9.5, 10.5);
    }

    [Fact]
    public void Up_and_down_gives_one_climb_per_ascent_and_none_for_small_bumps()
    {
        // up 150 m, down 150 m, up 30 m (too small), then flat
        double Elevation(int i) => i switch
        {
            < 150 => 1000 + i,
            < 300 => 1150 - (i - 150),
            < 330 => 1000 + (i - 300),
            _ => 1030,
        };
        var climbs = ElevationProfile.Build(Track(400, Elevation)).Analysis.Climbs;

        var climb = Assert.Single(climbs);
        Assert.InRange(climb.Gain, 140, 151);
        Assert.InRange(climb.StartDistance, 0, 30);
    }

    [Fact]
    public void Without_times_there_is_no_speed()
    {
        var analysis = ElevationProfile.Build(Track(50, i => 500, times: false)).Analysis;

        Assert.False(analysis.HasSpeeds);
        Assert.All(analysis.Slopes.Skip(5).SkipLast(5), s => Assert.Equal(0, s!.Value, 6));
    }

    [Fact]
    public void Tracks_are_coloured_by_fixed_slope_classes_and_by_a_ramp_of_their_own_elevations()
    {
        var track = Track(100, i => 1000 + i * 2.5); // ~22 % ramp

        var slope = TrackColoring.Classify(track, TrackColorMode.Slope)!;
        Assert.Equal(TrackColoring.SlopeClass(22), slope[50]);

        var elevation = TrackColoring.Classify(track, TrackColorMode.Elevation)!;
        Assert.Equal(0, elevation[0]);
        Assert.Equal(TrackColoring.RampSteps - 1, elevation[^1]);
        Assert.Null(TrackColoring.Classify(track, TrackColorMode.None));
        Assert.Null(TrackColoring.Classify(Track(10, _ => 0, times: false), TrackColorMode.Speed));
    }

    [Fact]
    public void Slope_classes_cover_downhill_flat_and_steep()
    {
        Assert.Equal(0, TrackColoring.SlopeClass(-40));
        Assert.Equal(3, TrackColoring.SlopeClass(0));
        Assert.Equal(TrackColoring.SlopeClasses.Count - 1, TrackColoring.SlopeClass(60));
        Assert.Equal(new Rgb(40, 90, 220), TrackColoring.RampColor(0));
        Assert.Equal(new Rgb(215, 40, 30), TrackColoring.RampColor(TrackColoring.RampSteps - 1));
    }
}
