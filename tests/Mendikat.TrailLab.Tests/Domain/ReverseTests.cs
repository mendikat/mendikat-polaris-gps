using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Domain.Model;
using Mendikat.TrailLab.Domain.Processing;

namespace Mendikat.TrailLab.Tests.Domain;

public sealed class ReverseTests
{
    private static readonly DateTimeOffset Start = new(2026, 5, 1, 7, 0, 0, TimeSpan.Zero);

    private static TrackSegment Segment(params int[] seconds)
    {
        var segment = new TrackSegment();
        segment.Points.AddRange(seconds.Select((s, i) => new TrailPoint(new GeoPosition(42.8 + i * 0.001, -1.6))
        {
            Time = s < 0 ? null : Start.AddSeconds(s),
            Elevation = 500 + s,
        }));
        return segment;
    }

    [Fact]
    [Trait("Req", "REV-01")]
    [Trait("Req", "REV-02")]
    public void Segments_and_points_are_reversed_and_times_mirrored()
    {
        var track = new Track();
        track.Segments.Add(Segment(0, 60, 180));
        track.Segments.Add(Segment(600, 660));
        var original = track.AllPoints.ToList();
        var segments = track.Segments.ToList();

        TrackReverser.Reverse(track).Do();

        Assert.Equal([segments[1], segments[0]], track.Segments);
        Assert.Equal(Enumerable.Reverse(original), track.AllPoints, ReferenceEqualityComparer.Instance);
        // Same start and end, same intervals: 0, 60, 660-180=480, 600, 660.
        Assert.Equal([0, 60, 480, 600, 660], track.AllPoints.Select(p => (p.Time!.Value - Start).TotalSeconds));
        Assert.Equal([1160, 1100, 680, 560, 500], track.AllPoints.Select(p => p.Elevation!.Value)); // other fields kept
    }

    [Fact]
    [Trait("Req", "REV-03")]
    public void Undo_restores_order_and_times()
    {
        var track = new Track();
        track.Segments.Add(Segment(0, 60, 180));
        track.Segments.Add(Segment(600, 660));
        var original = track.AllPoints.Select(p => (p, p.Time)).ToList();
        var segments = track.Segments.ToList();
        var command = TrackReverser.Reverse(track);

        command.Do();
        command.Undo();

        Assert.Equal(segments, track.Segments);
        Assert.Equal(original, track.AllPoints.Select(p => (p, p.Time)));
    }

    [Fact]
    [Trait("Req", "REV-02")]
    public void Points_without_time_keep_having_none()
    {
        var route = new Route();
        route.Points.AddRange(Segment(-1, 0, -1, 100).Points);

        TrackReverser.Reverse(route).Do();

        Assert.Equal([0, null, 100, null],route.Points.Select(p => p.Time is { } t ? (t - Start).TotalSeconds : (double?)null));
    }

    [Fact]
    [Trait("Req", "REV-01")]
    public void Empty_and_single_point_tracks_are_handled()
    {
        var track = new Track();
        TrackReverser.Reverse(track).Do();
        track.Segments.Add(Segment(0));
        TrackReverser.Reverse(track).Do();

        Assert.Equal(0, (track.AllPoints.Single().Time!.Value - Start).TotalSeconds);
    }
}
