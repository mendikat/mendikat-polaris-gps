using Polaris.Gps.Domain.Documents;
using Polaris.Gps.Gpx;

namespace Polaris.Gps.Tests.Gpx;

public sealed class GpxReaderTests
{
    private static TrailFileResult Read(string fixture)
    {
        using var stream = Fixture.Open($"gpx/{fixture}");
        return new GpxReader().Read(stream, TestContext.Current.CancellationToken);
    }

    private static DocumentLoadException ReadFails(string fixture) =>
        Assert.Throws<DocumentLoadException>(() => Read(fixture));

    [Fact]
    public void Valid_gpx_11_loads_tracks_segments_routes_and_waypoints()
    {
        var result = Read("valid-11.gpx");
        var data = result.Data;

        Assert.Empty(result.Warnings);
        Assert.Equal(2, data.Waypoints.Count);
        var route = Assert.Single(data.Routes);
        Assert.Equal("Ruta planificada", route.Name);
        Assert.Equal(3, route.Points.Count);
        var track = Assert.Single(data.Tracks);
        Assert.Equal("Subida a Txindoki", track.Name);
        Assert.Equal("hiking", track.Type);
        Assert.Equal([3, 2], track.Segments.Select(s => s.Points.Count));
    }

    [Fact]
    public void Standard_point_fields_are_typed()
    {
        var waypoint = Read("valid-11.gpx").Data.Waypoints[0];

        Assert.Equal(42.9853, waypoint.Position.Latitude);
        Assert.Equal(-2.0786, waypoint.Position.Longitude);
        Assert.Equal(1431, waypoint.Elevation);
        Assert.Equal("Txindoki", waypoint.Name);
        Assert.Equal("Cima", waypoint.Comment);
        Assert.Equal("Cumbre de Txindoki", waypoint.Description);
        Assert.Equal("Summit", waypoint.Symbol);
    }

    [Fact]
    public void Unknown_fields_and_extensions_are_kept_verbatim_with_their_namespaces()
    {
        var data = Read("valid-11.gpx").Data;
        var point = data.Tracks[0].Segments[0].Points[0];

        Assert.Equal(["sat", "hdop", "extensions"], point.OtherElements.Select(e => e.LocalName));
        var extension = point.OtherElements[2].Xml;
        Assert.Contains("gpxtpx:TrackPointExtension", extension);
        Assert.Contains("xmlns:gpxtpx=\"http://www.garmin.com/xmlschemas/TrackPointExtension/v1\"", extension);
        Assert.Contains("<gpxtpx:hr>110</gpxtpx:hr>", extension);

        var state = Assert.IsType<GpxFormatState>(data.FormatState);
        Assert.Equal("1.1", state.SourceVersion);
        Assert.Equal("Mendikat test", state.Creator);
        Assert.NotNull(state.Metadata);
        Assert.Contains("<name>Aralar</name>", state.Metadata.Xml);
        Assert.Single(state.OtherElements, e => e.LocalName == "extensions");
        Assert.Contains(state.OtherAttributes, a => a.LocalName == "schemaLocation");
        Assert.Contains(state.NamespaceDeclarations, n => n.Key == "gpxtpx");
    }

    [Fact]
    public void Times_are_read_as_utc_instants_including_offsets()
    {
        var points = Read("valid-11.gpx").Data.Tracks[0].Segments[0].Points;

        Assert.Equal(new DateTimeOffset(2026, 5, 1, 7, 0, 0, TimeSpan.Zero), points[0].Time);
        Assert.Equal(new DateTimeOffset(2026, 5, 1, 7, 30, 0, TimeSpan.Zero), points[1].Time); // 09:30+02:00
        Assert.Equal(TimeSpan.Zero, points[1].Time!.Value.Offset);
    }

    [Fact]
    public void Times_without_zone_are_read_as_utc_with_a_warning()
    {
        var result = Read("no-zone-time.gpx");

        Assert.Equal(new DateTimeOffset(2026, 5, 1, 7, 0, 0, TimeSpan.Zero), result.Data.Waypoints[0].Time);
        Assert.Equal(new LoadWarning(LoadWarningKind.TimesWithoutZone, 2), Assert.Single(result.Warnings));
    }

    [Fact]
    public void Missing_elevation_and_time_are_null_not_zero()
    {
        var points = Read("missing-data.gpx").Data.Tracks[0].Segments[0].Points;

        Assert.All(points, p => Assert.Null(p.Elevation));
        Assert.Null(points[0].Time);
        Assert.NotNull(points[2].Time);
    }

    [Fact]
    public void Gpx_10_is_read_with_a_warning_and_keeps_its_extra_fields()
    {
        var result = Read("gpx10.gpx");

        Assert.Contains(new LoadWarning(LoadWarningKind.Gpx10), result.Warnings);
        var point = result.Data.Tracks[0].Segments[0].Points[0];
        Assert.Equal(10, point.Elevation);
        Assert.Equal(["course", "speed"], point.OtherElements.Select(e => e.LocalName));
        Assert.Equal("1.0", Assert.IsType<GpxFormatState>(result.Data.FormatState).SourceVersion);
    }

    [Fact]
    public void Gpx_without_namespace_is_read_with_a_warning()
    {
        var result = Read("no-namespace.gpx");

        Assert.Single(result.Data.Waypoints);
        Assert.Contains(new LoadWarning(LoadWarningKind.GpxWithoutNamespace), result.Warnings);
    }

    [Fact]
    public void Gpx_without_points_loads_with_a_warning()
    {
        Assert.Contains(new LoadWarning(LoadWarningKind.NoData), Read("no-points.gpx").Warnings);
    }

    [Fact]
    public void Malformed_xml_reports_invalid_xml_with_line()
    {
        var ex = ReadFails("malformed.gpx");

        Assert.Equal(DocumentLoadError.InvalidXml, ex.Error);
        Assert.Equal(6, ex.Line);
    }

    [Fact]
    public void Blank_file_reports_empty()
    {
        Assert.Equal(DocumentLoadError.EmptyFile, ReadFails("blank.gpx").Error);
    }

    [Fact]
    public void Latitude_out_of_range_is_rejected_with_line_and_value()
    {
        var ex = ReadFails("out-of-range.gpx");

        Assert.Equal(DocumentLoadError.InvalidCoordinate, ex.Error);
        Assert.Equal(4, ex.Line);
        Assert.Equal("lat=\"95\"", ex.Detail);
    }

    [Theory]
    [InlineData("not-gpx.gpx", DocumentLoadError.NotGpx)]
    [InlineData("other-namespace.gpx", DocumentLoadError.NotGpx)]
    [InlineData("bad-elevation.gpx", DocumentLoadError.InvalidNumber)]
    public void Unsupported_structures_are_rejected(string fixture, DocumentLoadError expected)
    {
        Assert.Equal(expected, ReadFails(fixture).Error);
    }

    [Fact]
    public void Dtd_is_not_processed()
    {
        const string xml = """
            <?xml version="1.0"?>
            <!DOCTYPE gpx [<!ENTITY x "boom">]>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1"><wpt lat="1" lon="1"><name>&x;</name></wpt></gpx>
            """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));

        var ex = Assert.Throws<DocumentLoadException>(() => new GpxReader().Read(stream, TestContext.Current.CancellationToken));

        Assert.Equal(DocumentLoadError.InvalidXml, ex.Error);
    }

    [Fact]
    public void Large_track_of_100000_points_is_read_quickly()
    {
        var builder = new System.Text.StringBuilder("<gpx version=\"1.1\" xmlns=\"http://www.topografix.com/GPX/1/1\"><trk><trkseg>");
        for (var i = 0; i < 100_000; i++)
        {
            builder.Append(System.Globalization.CultureInfo.InvariantCulture,
                $"<trkpt lat=\"{42 + i * 1e-6}\" lon=\"{-1.6 + i * 1e-6}\"><ele>{500 + i % 100}</ele><time>2026-05-01T07:00:00Z</time></trkpt>");
        }

        builder.Append("</trkseg></trk></gpx>");
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(builder.ToString()));
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var data = new GpxReader().Read(stream, TestContext.Current.CancellationToken).Data;

        Assert.Equal(100_000, data.Tracks[0].PointCount);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"took {watch.Elapsed}");
    }
}
