using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Domain.Processing;

namespace Polaris.Gps.Tests.Domain;

/// <summary>Tools › split at the selected point, remove the points before or after it, segments to tracks.</summary>
public sealed class CutTests
{
    private static TrackSegment Segment(int first, int count)
    {
        var segment = new TrackSegment();
        segment.Points.AddRange(Enumerable.Range(first, count).Select(i => new TrailPoint(new GeoPosition(42.8 + i * 0.001, -1.6)) { Name = $"p{i}" }));
        return segment;
    }

    /// <summary>A track "T" with segments p0..p3 and p4..p6.</summary>
    private static (TrailData Data, Track Track) TwoSegments()
    {
        var data = new TrailData();
        var track = new Track { Name = "T", Description = "d" };
        track.Segments.Add(Segment(0, 4));
        track.Segments.Add(Segment(4, 3));
        data.Tracks.Add(track);
        data.Tracks.Add(new Track { Name = "Other" });
        return (data, track);
    }

    private static string[] Names(Track track) => track.Segments.Select(s => string.Join(",", s.Points.Select(p => p.Name))).ToArray();

    [Fact]
    public void Splitting_inside_a_segment_copies_the_point_to_the_second_track_placed_right_after()
    {
        var (data, track) = TwoSegments();
        var point = track.Segments[0].Points[1];

        var (command, second) = TrackCutter.Split(data, track, point)!.Value;
        command.Do();

        Assert.Equal(["p0,p1"], Names(track));
        Assert.Equal(["p1,p2,p3", "p4,p5,p6"], Names(second));
        Assert.NotSame(point, second.Segments[0].Points[0]);
        Assert.Equal("T (2)", second.Name);
        Assert.Equal("d", second.Description);
        Assert.Equal(["T", "T (2)", "Other"], data.Tracks.Select(t => t.Name));

        command.Undo();
        Assert.Equal(["p0,p1,p2,p3", "p4,p5,p6"], Names(track));
        Assert.Equal(["T", "Other"], data.Tracks.Select(t => t.Name));
    }

    [Fact]
    public void Splitting_at_the_end_of_a_segment_starts_the_second_track_with_the_next_segment()
    {
        var (data, track) = TwoSegments();

        var (command, second) = TrackCutter.Split(data, track, track.Segments[0].Points[3])!.Value;
        command.Do();

        Assert.Equal(["p0,p1,p2,p3"], Names(track));
        Assert.Equal(["p4,p5,p6"], Names(second));
    }

    [Fact]
    public void The_first_and_last_points_cannot_split()
    {
        var (data, track) = TwoSegments();

        Assert.Null(TrackCutter.Split(data, track, track.Segments[0].Points[0]));
        Assert.Null(TrackCutter.Split(data, track, track.Segments[1].Points[2]));
    }

    [Fact]
    public void Routes_split_like_tracks()
    {
        var data = new TrailData();
        var route = new Route { Name = "R" };
        route.Points.AddRange(Segment(0, 5).Points);
        data.Routes.Add(route);

        var (command, second) = TrackCutter.Split(data, route, route.Points[2])!.Value;
        command.Do();

        Assert.Equal(["p0", "p1", "p2"], route.Points.Select(p => p.Name));
        Assert.Equal(["p2", "p3", "p4"], second.Points.Select(p => p.Name));
        Assert.Equal(["R", "R (2)"], data.Routes.Select(r => r.Name));
    }

    [Fact]
    public void Removing_before_and_after_trims_the_whole_track_including_other_segments()
    {
        var (_, track) = TwoSegments();
        var point = track.Segments[1].Points[1]; // p5

        var (before, removedBefore) = TrackCutter.RemoveBefore(track, point)!.Value;
        before.Do();
        Assert.Equal(5, removedBefore);
        Assert.Equal(["p5,p6"], Names(track));

        var (after, removedAfter) = TrackCutter.RemoveAfter(track, point)!.Value;
        after.Do();
        Assert.Equal(1, removedAfter);
        Assert.Equal(["p5"], Names(track));

        after.Undo();
        before.Undo();
        Assert.Equal(["p0,p1,p2,p3", "p4,p5,p6"], Names(track));
        Assert.Null(TrackCutter.RemoveBefore(track, track.Segments[0].Points[0]));
    }

    [Fact]
    public void Each_segment_becomes_a_track()
    {
        var (data, track) = TwoSegments();

        var (command, created) = TrackCutter.SegmentsToTracks(data, track)!.Value;
        command.Do();

        Assert.Equal(["p0,p1,p2,p3"], Names(track));
        Assert.Equal(["p4,p5,p6"], Names(Assert.Single(created)));
        Assert.Equal(["T", "T (2)", "Other"], data.Tracks.Select(t => t.Name));

        command.Undo();
        Assert.Equal(2, track.Segments.Count);
        Assert.Equal(2, data.Tracks.Count);
        Assert.Null(TrackCutter.SegmentsToTracks(data, data.Tracks[1]));
    }
}
