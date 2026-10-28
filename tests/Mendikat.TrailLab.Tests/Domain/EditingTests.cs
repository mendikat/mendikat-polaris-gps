using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Editing;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Domain.Model;

namespace Mendikat.TrailLab.Tests.Domain;

public sealed class EditingTests
{
    internal static List<TrailPoint> Points(int count, bool withData = true) =>
        Enumerable.Range(0, count).Select(i => new TrailPoint(new GeoPosition(42 + i * 0.001, -1.6))
        {
            Elevation = withData ? 500 + i * 10 : null,
            Time = withData ? new DateTimeOffset(2026, 5, 1, 7, i, 0, TimeSpan.Zero) : null,
        }).ToList();

    [Fact]
    public void Geodesic_distance_of_one_degree_on_the_equator()
    {
        Assert.Equal(111_319.491, Geodesy.Distance(new GeoPosition(0, 0), new GeoPosition(0, 1)), 0.001);
    }

    [Fact]
    public void Midpoint_is_halfway_along_the_geodesic_within_a_centimeter()
    {
        var a = new GeoPosition(42.9921, -2.0610);
        var b = new GeoPosition(42.9853, -2.0786);

        var m = Geodesy.Midpoint(a, b);

        var total = Geodesy.Distance(a, b);
        Assert.Equal(total / 2, Geodesy.Distance(a, m), 0.01);
        Assert.Equal(total / 2, Geodesy.Distance(m, b), 0.01);
    }

    [Fact]
    public void Midpoint_across_the_antimeridian_has_a_valid_longitude()
    {
        var m = Geodesy.Midpoint(new GeoPosition(0, 179.9), new GeoPosition(0, -179.9));

        Assert.Equal(180, Math.Abs(m.Longitude), 1e-9);
    }

    [Fact]
    public void Insert_after_a_middle_point_interpolates_elevation_and_time()
    {
        var points = Points(3);

        var command = PointEditing.CreateInsertion(points, points[0])!;
        command.Do();

        Assert.Equal(4, points.Count);
        var added = points[1];
        Assert.Same(command.Point, added);
        Assert.True(added.IsAdded);
        Assert.Equal(505, added.Elevation);
        Assert.Equal(new DateTimeOffset(2026, 5, 1, 7, 0, 30, TimeSpan.Zero), added.Time);
    }

    [Fact]
    public void Insert_on_the_last_point_goes_between_the_previous_one_and_it()
    {
        var points = Points(3);
        var last = points[2];

        PointEditing.CreateInsertion(points, last)!.Do();

        Assert.Equal(4, points.Count);
        Assert.Same(last, points[3]);
        Assert.True(points[2].IsAdded);
    }

    [Fact]
    public void Insert_without_data_on_both_sides_leaves_elevation_and_time_empty()
    {
        var points = Points(2);
        points[1].Elevation = null;
        points[1].Time = null;

        PointEditing.CreateInsertion(points, points[0])!.Do();

        Assert.Null(points[1].Elevation);
        Assert.Null(points[1].Time);
    }

    [Fact]
    public void Insert_needs_two_points()
    {
        var points = Points(1);

        Assert.Null(PointEditing.CreateInsertion(points, points[0]));
    }

    [Fact]
    public void Removing_points_and_undoing_restores_the_same_objects_in_order()
    {
        var points = Points(6);
        var original = points.ToList();
        var command = new RemovePointsCommand(points, [points[1], points[4], points[2]]);

        command.Do();
        Assert.Equal([original[0], original[3], original[5]], points);

        command.Undo();
        Assert.Equal(original, points);
    }

    [Fact]
    public void Editing_a_point_and_undoing_restores_every_value()
    {
        var point = Points(1)[0];
        point.Name = "A";
        var before = PointValues.From(point);
        var command = new EditPointCommand(point, before with { Position = new GeoPosition(43, -2), Name = "B", Elevation = null });

        command.Do();
        Assert.Equal("B", point.Name);
        Assert.Null(point.Elevation);

        command.Undo();
        Assert.Equal(before, PointValues.From(point));
    }

    [Fact]
    public void Removing_a_track_and_undoing_puts_it_back_in_place()
    {
        var tracks = new List<Track> { new() { Name = "1" }, new() { Name = "2" }, new() { Name = "3" } };
        var second = tracks[1];
        var command = new RemoveItemCommand<Track>(tracks, second);

        command.Do();
        Assert.DoesNotContain(second, tracks);

        command.Undo();
        Assert.Same(second, tracks[1]);
    }

    [Fact]
    public void Undoing_back_to_the_saved_state_clears_the_unsaved_indicator()
    {
        var data = new TrailData();
        data.Waypoints.AddRange(Points(3));
        var document = new TrailDocument(@"C:\data\w.gpx", DocumentFormat.Gpx, data);

        document.Execute(new RemovePointsCommand(data.Waypoints, [data.Waypoints[0]]));
        Assert.True(document.IsDirty);
        Assert.True(document.CanUndo);

        document.Undo();
        Assert.False(document.IsDirty);
        Assert.True(document.CanRedo);

        document.Redo();
        Assert.True(document.IsDirty);

        document.MarkSaved(@"C:\data\w.gpx");
        Assert.False(document.IsDirty);
        document.Undo();
        Assert.True(document.IsDirty); // the file now has the deletion; the document no longer does
    }

    [Fact]
    public void A_new_edit_after_undo_discards_the_redo_history()
    {
        var data = new TrailData();
        data.Waypoints.AddRange(Points(3));
        var document = new TrailDocument(@"C:\data\w.gpx", DocumentFormat.Gpx, data);
        document.Execute(new RemovePointsCommand(data.Waypoints, [data.Waypoints[0]]));
        document.Undo();

        document.Execute(new RemovePointsCommand(data.Waypoints, [data.Waypoints[2]]));

        Assert.False(document.CanRedo);
        Assert.False(document.Redo());
    }

    [Fact]
    public void Every_operation_raises_exactly_one_change_event()
    {
        var data = new TrailData();
        data.Waypoints.AddRange(Points(3));
        var document = new TrailDocument(@"C:\data\w.gpx", DocumentFormat.Gpx, data);
        var events = new List<bool>();
        document.Changed += (_, e) => events.Add(e.ContentChanged);

        document.Execute(new CompositeCommand(
        [
            new RemovePointsCommand(data.Waypoints, [data.Waypoints[0]]),
            new RemovePointsCommand(data.Waypoints, [data.Waypoints[1]]),
        ]));
        document.Undo();
        document.MarkSaved(@"C:\data\w.gpx");

        Assert.Equal([true, true, false], events);
        Assert.Equal(3, data.Waypoints.Count);
    }

    [Fact]
    public void Locate_finds_the_list_and_item_of_a_point()
    {
        var data = new TrailData();
        var track = new Track();
        track.Segments.Add(new TrackSegment());
        track.Segments.Add(new TrackSegment());
        track.Segments[1].Points.AddRange(Points(2));
        data.Tracks.Add(track);
        data.Waypoints.AddRange(Points(1));

        var location = data.Locate(track.Segments[1].Points[1])!;

        Assert.Same(track, location.Item);
        Assert.Same(track.Segments[1].Points, location.Points);
        Assert.Equal(1, location.SegmentIndex);
        Assert.Same(data.Waypoints, data.Locate(data.Waypoints[0])!.Item);
        Assert.Null(data.Locate(new TrailPoint(new GeoPosition(0, 0))));
    }
}
