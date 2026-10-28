using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Model;
using Mendikat.TrailLab.Gpx;

namespace Mendikat.TrailLab.Tests.Gpx;

/// <summary>KML, KMZ and TCX read and converted to GPX data (increment 10).</summary>
public sealed class ConversionReaderTests
{
    private static readonly XNamespace Gpx = GpxReader.Gpx11Namespace;
    private static readonly XNamespace Tpx = TcxReader.TrackPointExtensionNamespace;

    private static SourceFileResult ReadFixture(string name)
    {
        using var stream = Fixture.Open("convert/" + name);
        return TrailFileReader.Read(stream, name, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static TrailFileResult ReadKml() => ReadFixture("sample.kml").Result;

    /// <summary>A KMZ in memory with the given entries (name → content).</summary>
    private static MemoryStream Kmz(params (string Name, byte[] Content)[] entries)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var entry = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
                entry.Write(content);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static byte[] SampleKmlBytes() => File.ReadAllBytes(Fixture.PathOf("convert/sample.kml"));

    [Fact]
    public void Kml_points_become_waypoints_with_name_description_and_optional_elevation()
    {
        var data = ReadKml().Data;

        Assert.Equal(2, data.Waypoints.Count);
        var summit = data.Waypoints[0];
        Assert.Equal("Cima", summit.Name);
        Assert.Equal("Buzón", summit.Description);
        Assert.Equal(42.812, summit.Position.Latitude, 9); // lon,lat order: not swapped (D11)
        Assert.Equal(-1.645, summit.Position.Longitude, 9);
        Assert.Equal(1234.5, summit.Elevation);
        Assert.Equal("Dos tramos", data.Waypoints[1].Name);
        Assert.Null(data.Waypoints[1].Elevation);
    }

    [Fact]
    public void Kml_lines_of_every_folder_become_tracks_with_one_segment_per_line()
    {
        var tracks = ReadKml().Data.Tracks;

        Assert.Equal(["Línea sin altitud", "Dos tramos", "Grabado", "Varias grabaciones"], tracks.Select(t => t.Name));
        Assert.Equal([[3], [2, 3], [2], [2, 1]], tracks.Select(t => t.Segments.Select(s => s.Points.Count).ToArray()));

        var plain = tracks[0].Segments[0].Points;
        Assert.All(plain, p => Assert.Null(p.Elevation)); // no altitude → no elevation, never 0 m (D11)
        Assert.Equal([42.80, 42.81, 42.82], plain.Select(p => p.Position.Latitude));
        Assert.Equal([500.0, 510, 520, 530, 540], tracks[1].AllPoints.Select(p => p.Elevation!.Value));
    }

    [Fact]
    public void Google_earth_tracks_keep_their_times()
    {
        var recorded = ReadKml().Data.Tracks[2].Segments[0].Points;

        Assert.Equal(
            [new DateTimeOffset(2026, 5, 1, 7, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 5, 1, 7, 0, 10, TimeSpan.Zero)],
            recorded.Select(p => p.Time!.Value));
        Assert.Equal([600.0, 610], recorded.Select(p => p.Elevation!.Value));
        Assert.Equal(-1.81, recorded[1].Position.Longitude, 9);
    }

    [Fact]
    public void Kml_conversion_reports_its_source_and_what_was_ignored()
    {
        var result = ReadKml();

        Assert.Equal(
            [new LoadWarning(LoadWarningKind.ConvertedFrom, Detail: "KML"), new LoadWarning(LoadWarningKind.IgnoredFeatures, 2)],
            result.Warnings); // one polygon and one ground overlay
        var state = Assert.IsType<GpxFormatState>(result.Data.FormatState);
        Assert.Equal("KML", state.ConvertedFrom);
        var metadata = XElement.Parse(state.Metadata!.Xml);
        Assert.Equal("Excursión de prueba", (string?)metadata.Element(Gpx + "name"));
        Assert.Equal("Archivo de referencia de TrailLab", (string?)metadata.Element(Gpx + "desc"));
    }

    [Fact]
    public void Kml_from_mendikat_net_with_spaces_after_commas_and_latin1_is_read()
    {
        var data = ReadFixture("mendikat-net.kml").Result.Data;

        var waypoint = Assert.Single(data.Waypoints);
        Assert.Equal("Tentudía", waypoint.Name);
        Assert.Equal((38.04557, -6.36669, 1063.1424), (waypoint.Position.Latitude, waypoint.Position.Longitude, waypoint.Elevation!.Value));
        var points = Assert.Single(data.Tracks).AllPoints.ToList();
        Assert.Equal([38.04557, 38.04558, 38.04559], points.Select(p => p.Position.Latitude));
        Assert.Equal([1063.1, 1064.2, 1065.3], points.Select(p => p.Elevation!.Value));
    }

    [Fact]
    public void A_kml_point_uses_its_first_tuple_like_google_earth()
    {
        // Found in mendikat.net's files: a point followed by a truncated second tuple.
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            "<kml xmlns=\"http://earth.google.com/kml/2.0\"><Placemark><Point><coordinates>\n\t-3.202802,43.272921,548 -3.203456\n</coordinates></Point></Placemark></kml>"));

        var point = Assert.Single(new KmlReader().Read(stream, cancellationToken: TestContext.Current.CancellationToken).Data.Waypoints);

        Assert.Equal((43.272921, -3.202802, 548.0), (point.Position.Latitude, point.Position.Longitude, point.Elevation!.Value));
    }

    [Fact]
    public void Invalid_kml_coordinates_are_rejected_with_their_line()
    {
        var ex = Assert.Throws<DocumentLoadException>(() => ReadFixture("bad-coordinate.kml"));

        Assert.Equal(DocumentLoadError.InvalidCoordinate, ex.Error);
        Assert.Equal(2, ex.Line);
    }

    [Fact]
    public void Kmz_reads_doc_kml_like_its_kml_and_ignores_images()
    {
        using var kmz = Kmz(("images/icono.png", [1, 2, 3]), ("otro.kml", "<kml/>"u8.ToArray()), ("doc.kml", SampleKmlBytes()));

        var (format, result) = TrailFileReader.Read(kmz, "excursion.kmz", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(SourceFormat.Kmz, format);
        var kml = ReadKml().Data;
        Assert.Equal(kml.Tracks.Select(t => t.PointCount), result.Data.Tracks.Select(t => t.PointCount));
        Assert.Equal(kml.Waypoints.Count, result.Data.Waypoints.Count);
        Assert.Equal(new LoadWarning(LoadWarningKind.ConvertedFrom, Detail: "KMZ"), result.Warnings[0]);
    }

    [Fact]
    public void Kmz_without_doc_kml_uses_the_first_kml()
    {
        using var kmz = Kmz(("files/ruta.kml", SampleKmlBytes()));

        Assert.Equal(4, TrailFileReader.Read(kmz, "ruta.kmz", cancellationToken: TestContext.Current.CancellationToken).Result.Data.Tracks.Count);
    }

    [Theory]
    [InlineData("no-kml")]
    [InlineData("broken-zip")]
    [InlineData("bomb")]
    public void Invalid_kmz_files_are_rejected(string kind)
    {
        using var kmz = kind switch
        {
            "no-kml" => Kmz(("foto.jpg", [1, 2, 3])),
            "bomb" => Kmz(("doc.kml", new byte[50 * 1024 * 1024])), // 50 MB of zeros: ratio far above 200
            _ => new MemoryStream(File.ReadAllBytes(Fixture.PathOf("convert/broken.kmz"))),
        };

        var ex = Assert.Throws<DocumentLoadException>(() => TrailFileReader.Read(kmz, "x.kmz", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(DocumentLoadError.InvalidArchive, ex.Error);
    }

    [Fact]
    public void A_corrupt_kml_inside_a_kmz_is_reported_as_invalid_xml()
    {
        using var kmz = Kmz(("doc.kml", "<kml><Placemark>"u8.ToArray()));

        Assert.Equal(DocumentLoadError.InvalidXml, Assert.Throws<DocumentLoadException>(() => TrailFileReader.Read(kmz, "x.kmz", cancellationToken: TestContext.Current.CancellationToken)).Error);
    }

    [Fact]
    public void Tcx_activity_becomes_a_track_with_one_segment_per_lap()
    {
        var (format, result) = ReadFixture("sample.tcx");
        var activity = result.Data.Tracks[0];

        Assert.Equal(SourceFormat.Tcx, format);
        Assert.Equal("2026-05-01T07:00:00Z", activity.Name);
        Assert.Equal("Running", activity.Type);
        Assert.Equal([2, 2], activity.Segments.Select(s => s.Points.Count)); // the point without position is skipped
        var first = activity.Segments[0].Points[0];
        Assert.Equal(new DateTimeOffset(2026, 5, 1, 7, 0, 0, TimeSpan.Zero), first.Time);
        Assert.Equal(450.5, first.Elevation);
        Assert.Equal(42.8, first.Position.Latitude);
        Assert.Null(activity.Segments[1].Points[0].Elevation);
    }

    [Fact]
    public void Tcx_heart_rate_and_cadence_go_to_the_garmin_trackpoint_extension()
    {
        var points = ReadFixture("sample.tcx").Result.Data.Tracks[0].AllPoints.ToList();

        XElement? Extension(TrailPoint p) =>
            p.OtherElements.Select(f => XElement.Parse(f.Xml)).SingleOrDefault()?.Element(Tpx + "TrackPointExtension");

        Assert.Equal("120", (string?)Extension(points[0])!.Element(Tpx + "hr"));
        Assert.Equal("80", (string?)Extension(points[0])!.Element(Tpx + "cad"));
        Assert.Null(Extension(points[1])); // nothing to keep
        Assert.Equal("85", (string?)Extension(points[2])!.Element(Tpx + "cad")); // RunCadence of the TPX extension
        Assert.Null(Extension(points[2])!.Element(Tpx + "hr"));
    }

    [Fact]
    public void Tcx_reports_skipped_points_and_data_without_gpx_equivalent()
    {
        Assert.Equal(
            [
                new LoadWarning(LoadWarningKind.ConvertedFrom, Detail: "TCX"),
                new LoadWarning(LoadWarningKind.PointsWithoutPosition, 1),
                new LoadWarning(LoadWarningKind.DataNotConverted, 2), // DistanceMeters and Speed
            ],
            ReadFixture("sample.tcx").Result.Warnings);
    }

    [Fact]
    public void Tcx_courses_become_tracks_and_their_course_points_waypoints()
    {
        var data = ReadFixture("sample.tcx").Result.Data;

        Assert.Equal("Ruta del pantano", data.Tracks[1].Name);
        Assert.Equal(2, data.Tracks[1].PointCount);
        var fountain = Assert.Single(data.Waypoints);
        Assert.Equal(("Fuente", "Water", "Agua potable", 101.0), (fountain.Name, fountain.Type, fountain.Description, fountain.Elevation));
        Assert.Equal(new DateTimeOffset(2026, 5, 2, 8, 0, 30, TimeSpan.Zero), fountain.Time);
    }

    [Fact]
    public void Converted_tcx_is_written_as_valid_gpx_and_keeps_heart_rate()
    {
        var data = ReadFixture("sample.tcx").Result.Data;

        var bytes = new GpxWriter().Write(data);

        Assert.Empty(GpxWriterTests.SchemaErrors(bytes));
        var reread = new GpxReader().Read(new MemoryStream(bytes), cancellationToken: TestContext.Current.CancellationToken).Data;
        Assert.Equal(data.Tracks.Select(t => t.PointCount), reread.Tracks.Select(t => t.PointCount));
        var gpx = Encoding.UTF8.GetString(bytes);
        Assert.Contains("<gpxtpx:hr>120</gpxtpx:hr>", gpx);
        Assert.Contains("xmlns:gpxtpx=\"" + TcxReader.TrackPointExtensionNamespace + "\"", gpx);
    }

    [Fact]
    public void Converted_kml_is_written_as_valid_gpx_with_its_metadata()
    {
        var bytes = new GpxWriter().Write(ReadKml().Data);

        Assert.Empty(GpxWriterTests.SchemaErrors(bytes));
        Assert.Contains("<name>Excursión de prueba</name>", Encoding.UTF8.GetString(bytes));
    }

    [Theory]
    [InlineData("sample.kml", "ruta.xml", SourceFormat.Kml)]
    [InlineData("sample.tcx", "ruta.gpx", SourceFormat.Tcx)]
    public void The_format_is_detected_from_the_content_not_the_extension(string fixture, string fileName, SourceFormat expected)
    {
        using var stream = Fixture.Open("convert/" + fixture);

        Assert.Equal(expected, TrailFileReader.Detect(stream, fileName));
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void Other_xml_files_are_unsupported_and_named_by_their_root()
    {
        var ex = Assert.Throws<DocumentLoadException>(() => ReadFixture("other.xml"));

        Assert.Equal((DocumentLoadError.UnsupportedFormat, "html"), (ex.Error, ex.Detail));
    }

    [Theory]
    [InlineData("x.kml", DocumentLoadError.InvalidXml)]
    [InlineData("x.tcx", DocumentLoadError.InvalidXml)]
    [InlineData("x.gpx", DocumentLoadError.InvalidXml)]
    public void Files_that_are_not_xml_are_reported_by_the_reader_of_their_extension(string fileName, DocumentLoadError expected)
    {
        using var stream = new MemoryStream("esto no es XML"u8.ToArray());

        Assert.Equal(expected, Assert.Throws<DocumentLoadException>(() => TrailFileReader.Read(stream, fileName, cancellationToken: TestContext.Current.CancellationToken)).Error);
    }

    [Fact]
    public void Gpx_files_are_read_as_before()
    {
        using var stream = new MemoryStream(
            "<gpx version=\"1.1\" creator=\"t\" xmlns=\"http://www.topografix.com/GPX/1/1\"><wpt lat=\"42\" lon=\"-1\"/></gpx>"u8.ToArray());

        var (format, result) = TrailFileReader.Read(stream, "a.gpx", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(SourceFormat.Gpx, format);
        Assert.Null(Assert.IsType<GpxFormatState>(result.Data.FormatState).ConvertedFrom);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Free_paths_get_a_number()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"C:\d\ruta.gpx", @"C:\d\ruta (2).gpx" };

        Assert.Equal(@"C:\d\ruta (3).gpx", GpxConversion.FreePath(@"C:\d\ruta.gpx", taken.Contains));
        Assert.Equal(@"C:\d\otra.gpx", GpxConversion.FreePath(@"C:\d\otra.gpx", taken.Contains));
    }
}
