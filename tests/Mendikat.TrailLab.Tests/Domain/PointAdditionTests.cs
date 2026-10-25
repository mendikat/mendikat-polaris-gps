using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Editing;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Domain.Model;

namespace Mendikat.TrailLab.Tests.Domain;

/// <summary>New documents and points added by hand (APP-08).</summary>
public sealed class PointAdditionTests
{
    private static readonly GeoPosition P = new(43.0, -2.0);

    [Fact]
    [Trait("Req", "APP-08")]
    public void New_document_has_a_title_but_no_file()
    {
        var document = TrailDocument.CreateNew(DocumentFormat.Gpx, "Sin título 1.gpx", new TrailData());

        Assert.True(document.IsNew);
        Assert.Null(document.FilePath);
        Assert.Equal("Sin título 1.gpx", document.FileName);
        Assert.False(document.IsDirty);
        Assert.False(document.IsSameFile(@"C:\data\Sin título 1.gpx"));

        document.MarkSaved(@"C:\data\ruta.gpx");

        Assert.False(document.IsNew);
        Assert.Equal("ruta.gpx", document.FileName);
        Assert.True(document.IsSameFile(@"c:\DATA\ruta.gpx"));
    }

    [Fact]
    [Trait("Req", "APP-08")]
    public void Addition_to_a_route_appends_at_the_end()
    {
        var data = new TrailData();
        var route = new Route();
        route.Points.AddRange(EditingTests.Points(2));
        data.Routes.Add(route);

        var (command, point) = PointEditing.CreateAddition(data, route, after: null, P);
        command.Do();

        Assert.Same(point, route.Points[^1]);
        Assert.Equal(3, route.Points.Count);
        command.Undo();
        Assert.Equal(2, route.Points.Count);
    }

    [Fact]
    [Trait("Req", "APP-08")]
    public void Addition_to_the_waypoints_adds_a_waypoint()
    {
        var data = new TrailData();

        var (command, point) = PointEditing.CreateAddition(data, data.Waypoints, after: null, P);
        command.Do();

        Assert.Same(point, Assert.Single(data.Waypoints));
    }

    [Fact]
    [Trait("Req", "APP-08")]
    public void Addition_to_a_track_without_segments_creates_one()
    {
        var data = new TrailData();
        var track = new Track();
        data.Tracks.Add(track);

        var (command, point) = PointEditing.CreateAddition(data, track, after: null, P);
        command.Do();

        Assert.Same(point, Assert.Single(Assert.Single(track.Segments).Points));
        command.Undo();
        Assert.Empty(track.Segments);
    }

    [Fact]
    [Trait("Req", "APP-08")]
    public void Addition_after_a_point_goes_into_that_points_segment()
    {
        var data = new TrailData();
        var track = new Track();
        var first = new TrackSegment();
        first.Points.AddRange(EditingTests.Points(3));
        var second = new TrackSegment();
        second.Points.AddRange(EditingTests.Points(2));
        track.Segments.AddRange([first, second]);
        data.Tracks.Add(track);

        var (command, point) = PointEditing.CreateAddition(data, track, first.Points[1], P);
        command.Do();

        Assert.Same(point, first.Points[2]);
        Assert.Equal(2, second.Points.Count);
    }
}
