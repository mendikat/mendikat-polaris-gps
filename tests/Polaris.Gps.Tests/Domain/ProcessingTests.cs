using GeographicLib;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Domain.Processing;

namespace Polaris.Gps.Tests.Domain;

public sealed class ProcessingTests
{
    private static readonly TimeZoneInfo Madrid = TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");

    /// <summary>A point at <paramref name="meters"/> along azimuth <paramref name="azimuth"/> from <paramref name="origin"/>.</summary>
    private static GeoPosition Offset(GeoPosition origin, double azimuth, double meters)
    {
        Geodesic.WGS84.Direct(origin.Latitude, origin.Longitude, azimuth, meters, out double lat, out double lon);
        return new GeoPosition(lat, lon);
    }

    /// <summary>Points every 10 m eastwards with a lateral wiggle of ±<paramref name="noise"/> m.</summary>
    private static List<TrailPoint> NoisyLine(int count, double noise, GeoPosition? start = null)
    {
        var origin = start ?? new GeoPosition(42.8, -1.6);
        return Enumerable.Range(0, count)
            .Select(i => new TrailPoint(Offset(Offset(origin, 90, i * 10), 0, i % 2 == 0 ? noise : -noise)) { Elevation = 500 + i })
            .ToList();
    }

    [Fact]
    public void Nearly_straight_segments_keep_only_their_ends_and_stay_separate()
    {
        var first = NoisyLine(100, noise: 1);
        var second = NoisyLine(100, noise: 1, new GeoPosition(42.9, -1.6));
        var (a, b) = (first[0], first[^1]);

        var plan = TrackSimplifier.Plan([first, second], toleranceMeters: 5);
        plan.Command!.Do();

        Assert.Equal((200, 4), (plan.OriginalCount, plan.ResultCount));
        Assert.Equal([a, b], first);
        Assert.Equal(2, second.Count);
    }

    [Fact]
    public void Deviations_larger_than_the_tolerance_are_kept()
    {
        // An "L": 50 points east, then 50 points north. The corner deviates ~350 m from the chord.
        var corner = new GeoPosition(42.8, -1.6);
        var points = Enumerable.Range(0, 50).Select(i => new TrailPoint(Offset(corner, 270, (50 - i) * 10)))
            .Concat(Enumerable.Range(0, 50).Select(i => new TrailPoint(Offset(corner, 0, i * 10))))
            .ToList();

        var kept = TrackSimplifier.KeptIndices(points, toleranceMeters: 5);

        Assert.Equal([0, 50, 99], kept);
    }

    [Fact]
    public void Kept_points_are_unchanged_and_the_simplification_can_be_undone()
    {
        var points = NoisyLine(30, noise: 1);
        var original = points.ToList();
        var lastElevation = points[^1].Elevation;

        var plan = TrackSimplifier.Plan([points], 5);
        plan.Command!.Do();
        Assert.Same(original[^1], points[^1]);
        Assert.Equal(lastElevation, points[^1].Elevation);

        plan.Command.Undo();
        Assert.Equal(original, points);
    }

    [Fact]
    public void Short_degenerate_and_duplicated_segments_are_handled()
    {
        Assert.Empty(TrackSimplifier.KeptIndices([], 5));
        Assert.Equal([0, 1], TrackSimplifier.KeptIndices(NoisyLine(2, 0), 5));

        var same = Enumerable.Range(0, 20).Select(_ => new TrailPoint(new GeoPosition(42, -1))).ToList();
        Assert.Equal([0, 19], TrackSimplifier.KeptIndices(same, 5));

        // A closed loop (start = end): the far side must be kept, not collapsed onto the start.
        var center = new GeoPosition(42.8, -1.6);
        var loop = Enumerable.Range(0, 37).Select(i => new TrailPoint(Offset(center, i * 10 % 360, 200))).ToList();
        Assert.True(TrackSimplifier.KeptIndices(loop, 5).Count > 3);

        Assert.Null(TrackSimplifier.Plan([NoisyLine(2, 0)], 5).Command); // nothing to remove
    }

    [Fact]
    public void One_million_points_do_not_overflow_the_stack()
    {
        var random = new Random(1);
        var points = Enumerable.Range(0, 1_000_000)
            .Select(i => new TrailPoint(new GeoPosition(42 + i * 1e-6 + random.NextDouble() * 1e-5, -1.6 + i * 1e-6)))
            .ToList();

        var kept = TrackSimplifier.KeptIndices(points, 2);

        Assert.Equal(0, kept[0]);
        Assert.Equal(999_999, kept[^1]);
    }

    [Fact]
    public void Tolerance_must_be_positive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TrackSimplifier.Plan([NoisyLine(5, 0)], 0));
    }

    [Theory]
    [InlineData(6, SpeedUnit.KilometersPerHour, 6 / 3.6)]
    [InlineData(1.5, SpeedUnit.MetersPerSecond, 1.5)]
    [InlineData(10, SpeedUnit.MinutesPerKilometer, 1000 / 600.0)]
    public void Speed_units_convert_to_meters_per_second(double speed, SpeedUnit unit, double expected)
    {
        Assert.Equal(expected, TimeRegenerator.ToMetersPerSecond(speed, unit), 9);
    }

    [Fact]
    public void Times_follow_distance_and_speed_across_a_daylight_saving_change()
    {
        var p0 = new GeoPosition(42.8, -1.6);
        var p1 = Offset(p0, 0, 500);
        var p2 = Offset(p1, 0, 1000);
        List<TrailPoint> points = [new(p0), new(p1), new(p2)];
        var options = new TimeOptions(new DateTime(2026, 3, 29, 1, 30, 0), Madrid, TimeMode.ByDistance, 6, SpeedUnit.KilometersPerHour, TimeSpan.Zero);

        var plan = TimeRegenerator.Plan([points], options)!;
        plan.Command.Do();

        Assert.Equal(
            [new DateTimeOffset(2026, 3, 29, 0, 30, 0, TimeSpan.Zero), new(2026, 3, 29, 0, 35, 0, TimeSpan.Zero), new(2026, 3, 29, 0, 45, 0, TimeSpan.Zero)],
            points.Select(p => p.Time!.Value));
        Assert.All(points, p => Assert.Equal(TimeSpan.Zero, p.Time!.Value.Offset)); // stored in UTC
        Assert.Equal(TimeSpan.FromMinutes(15), plan.Duration);
    }

    [Fact]
    public void Uniform_mode_ignores_distance()
    {
        var points = NoisyLine(4, 0);
        var options = new TimeOptions(new DateTime(2026, 5, 1, 9, 0, 0), TimeZoneInfo.Utc, TimeMode.UniformInterval, 0, SpeedUnit.KilometersPerHour, TimeSpan.FromSeconds(5));

        TimeRegenerator.Plan([points], options)!.Command.Do();

        Assert.Equal([0, 5, 10, 15], points.Select(p => (p.Time!.Value - points[0].Time!.Value).TotalSeconds));
    }

    [Fact]
    public void Segment_gaps_are_travelled_by_default_and_can_be_skipped()
    {
        var a = new GeoPosition(42.8, -1.6);
        List<TrailPoint> first = [new(a), new(Offset(a, 0, 100))];
        List<TrailPoint> second = [new(Offset(a, 0, 1100)), new(Offset(a, 0, 1200))];
        var options = new TimeOptions(new DateTime(2026, 5, 1, 9, 0, 0), TimeZoneInfo.Utc, TimeMode.ByDistance, 1, SpeedUnit.MetersPerSecond, TimeSpan.Zero);

        var withGap = TimeRegenerator.Plan([first, second], options)!;
        var withoutGap = TimeRegenerator.Plan([first, second], options with { IncludeSegmentGaps = false })!;

        Assert.Equal(1200, withGap.Duration.TotalSeconds, 0.01);
        Assert.Equal(200, withoutGap.Duration.TotalSeconds, 0.01);
    }

    [Fact]
    public void Nonexistent_and_ambiguous_local_start_times_are_detected()
    {
        Assert.Equal(TimeStartIssue.Invalid, TimeRegenerator.CheckStart(new DateTime(2026, 3, 29, 2, 30, 0), Madrid));
        Assert.Null(TimeRegenerator.Plan([NoisyLine(2, 0)],
            new TimeOptions(new DateTime(2026, 3, 29, 2, 30, 0), Madrid, TimeMode.ByDistance, 4, SpeedUnit.KilometersPerHour, TimeSpan.Zero)));

        var ambiguous = TimeRegenerator.Plan([NoisyLine(2, 0)],
            new TimeOptions(new DateTime(2026, 10, 25, 2, 30, 0), Madrid, TimeMode.ByDistance, 4, SpeedUnit.KilometersPerHour, TimeSpan.Zero))!;
        Assert.Equal(TimeStartIssue.Ambiguous, ambiguous.StartIssue);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.Zero), ambiguous.StartUtc); // standard time (UTC+1)
    }

    [Fact]
    public void Invalid_speed_gives_no_plan_and_undo_restores_previous_times()
    {
        var points = NoisyLine(3, 0);
        points[0].Time = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var start = new DateTime(2026, 5, 1, 9, 0, 0);

        Assert.Null(TimeRegenerator.Plan([points], new TimeOptions(start, TimeZoneInfo.Utc, TimeMode.ByDistance, 0, SpeedUnit.KilometersPerHour, TimeSpan.Zero)));

        var plan = TimeRegenerator.Plan([points], new TimeOptions(start, TimeZoneInfo.Utc, TimeMode.ByDistance, 4, SpeedUnit.KilometersPerHour, TimeSpan.Zero))!;
        Assert.Equal(1, plan.PointsWithPreviousTime);
        plan.Command.Do();
        plan.Command.Undo();
        Assert.Equal(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), points[0].Time);
        Assert.Null(points[1].Time);
    }
}
