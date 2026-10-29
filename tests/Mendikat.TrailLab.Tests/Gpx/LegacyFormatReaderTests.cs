using System.Text;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Gpx;

namespace Mendikat.TrailLab.Tests.Gpx;

/// <summary>
/// OziExplorer (.plt, .wpt) and CompeGPS/TwoNav (.trk, .wpt) converted to GPX data. The mendikat-net.* fixtures are
/// the test files of mendikat.net (storage/tests/tracks) and the expected values those of its PHP tests.
/// </summary>
public sealed class LegacyFormatReaderTests
{
    private static SourceFileResult ReadFixture(string name)
    {
        using var stream = Fixture.Open("convert/" + name);
        return TrailFileReader.Read(stream, name, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static SourceFileResult ReadText(string text, string fileName, Encoding? encoding = null)
    {
        using var stream = new MemoryStream((encoding ?? Encoding.UTF8).GetBytes(text));
        return TrailFileReader.Read(stream, fileName, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public void OziExplorer_track_as_mendikat_net()
    {
        var read = ReadFixture("mendikat-net.plt");

        Assert.Equal(SourceFormat.OziExplorer, read.Format);
        var points = Assert.Single(Assert.Single(read.Result.Data.Tracks).Segments).Points;
        Assert.Equal(2167, points.Count);
        Assert.Equal(41.98822, points[0].Position.Latitude, 1e-5);
        Assert.Equal(-2.921754, points[0].Position.Longitude, 1e-5);
        Assert.Equal(41.99837, points[^1].Position.Latitude, 1e-5);
        Assert.Equal(-2.82684, points[^1].Position.Longitude, 1e-5);
        Assert.Equal(5049 * 0.3048, points[0].Elevation!.Value, 1e-9);
        // 43646.493808 days from midnight of 30/12/1899: the date and time written in the same line.
        Assert.Equal(new DateTimeOffset(2019, 6, 30, 11, 51, 5, TimeSpan.Zero), points[0].Time);
        Assert.Contains(read.Result.Warnings, w => w.Kind == LoadWarningKind.TimesWithoutZone && w.Count == 2167);
        Assert.Contains(read.Result.Warnings, w => w is { Kind: LoadWarningKind.ConvertedFrom, Detail: "OziExplorer" });
    }

    [Fact]
    public void OziExplorer_waypoints_as_mendikat_net()
    {
        var read = ReadFixture("mendikat-net-ozi.wpt");

        Assert.Equal(SourceFormat.OziExplorer, read.Format);
        var waypoints = read.Result.Data.Waypoints;
        Assert.Equal(28, waypoints.Count);
        Assert.Empty(read.Result.Data.Tracks);
        var first = waypoints[0];
        Assert.Equal("ARATZ", first.Name);
        Assert.Equal(42.920933, first.Position.Latitude, 1e-6);
        Assert.Equal(-2.304901, first.Position.Longitude, 1e-6);
        Assert.Equal("Aratz 1.446", first.Description);
        Assert.Equal(4744 * 0.3048, first.Elevation!.Value, 1e-9);
        Assert.Equal(new DateTimeOffset(2010, 3, 19, 9, 0, 0, TimeSpan.Zero), first.Time);
    }

    [Fact]
    public void CompeGps_track_as_mendikat_net()
    {
        var read = ReadFixture("mendikat-net.trk");

        Assert.Equal(SourceFormat.CompeGps, read.Format);
        var points = Assert.Single(Assert.Single(read.Result.Data.Tracks).Segments).Points;
        Assert.Equal(942, points.Count);
        Assert.Equal(42.67570596, points[0].Position.Latitude, 1e-9);
        Assert.Equal(-3.19739917, points[0].Position.Longitude, 1e-9);
        Assert.Equal(823.900024, points[0].Elevation);
        Assert.Equal(new DateTimeOffset(2003, 1, 2, 8, 7, 37, TimeSpan.Zero), points[0].Time);
        Assert.Contains(read.Result.Warnings, w => w is { Kind: LoadWarningKind.ConvertedFrom, Detail: "CompeGPS/TwoNav" });
    }

    [Fact]
    public void CompeGps_waypoints_in_ED50_are_converted_to_WGS84_as_mendikat_net()
    {
        var read = ReadFixture("mendikat-net-compegps.wpt");

        Assert.Equal(SourceFormat.CompeGps, read.Format);
        var waypoints = read.Result.Data.Waypoints;
        Assert.Equal(3, waypoints.Count);
        var first = waypoints[0];
        // mendikat.net (PHP 8.5, 2026-10-29): 42.878987973672, -2.276960029027. Its latitude stops iterating early (D12).
        Assert.Equal(42.878987973672, first.Position.Latitude, 1e-5);
        Assert.Equal(-2.276960029027, first.Position.Longitude, 1e-7);
        var (lat, lon) = Ed50.FromWgs84(first.Position.Latitude, first.Position.Longitude);
        Assert.Equal(42.88009, lat, 1e-5);
        Assert.Equal(-2.27567, lon, 1e-5);
        Assert.Equal("La_Leze", first.Name);
        Assert.Equal("Waypoint", first.Symbol);
        Assert.Equal("Summit", waypoints[1].Symbol);
        Assert.Equal(604.617432, first.Elevation);
        Assert.Equal(new DateTimeOffset(2015, 11, 12, 14, 30, 0, TimeSpan.Zero), first.Time);
    }

    [Fact]
    public void CompeGps_times_use_the_time_zone_of_the_file_and_keep_the_whole_description()
    {
        var read = ReadText(
            "G  WGS 84\r\nU  1\r\nL  +02:00:00\r\nW  Cima A 43.1ºN 2.5ºE 12-JUN-2020 10:30:00 1500.0 Buzón y vértice\r\n", "x.wpt");

        var waypoint = Assert.Single(read.Result.Data.Waypoints);
        Assert.Equal(new DateTimeOffset(2020, 6, 12, 8, 30, 0, TimeSpan.Zero), waypoint.Time);
        Assert.Equal(2.5, waypoint.Position.Longitude);
        Assert.Equal("Buzón y vértice", waypoint.Description);
        Assert.DoesNotContain(read.Result.Warnings, w => w.Kind == LoadWarningKind.TimesWithoutZone);
    }

    [Fact]
    public void CompeGps_without_date_or_with_old_dates_has_no_time_and_names_the_track()
    {
        var read = ReadText(
            "G  WGS 84\nN  Subida\nT  A 43.1ºN 2.5ºW 00-NUL-00 00:00:00 s 100.0\nT  A 43.2ºS 2.6ºW 01-JAN-90 00:00:00 s 101.0\n",
            "x.trk");

        var track = Assert.Single(read.Result.Data.Tracks);
        Assert.Equal("Subida", track.Name);
        var points = track.Segments[0].Points;
        Assert.All(points, p => Assert.Null(p.Time));
        Assert.Equal(-43.2, points[1].Position.Latitude);
    }

    [Fact]
    public void CompeGps_in_UTM_is_rejected_with_a_clear_error()
    {
        var error = Assert.Throws<DocumentLoadException>(() =>
            ReadText("G  WGS 84\nU  0\nT  A 30T 512345 4734567 12-JUN-20 10:30:00 s 100.0\n", "x.trk"));

        Assert.Equal(DocumentLoadError.UnsupportedDatum, error.Error);
        Assert.Equal("UTM", error.Detail);
        Assert.Equal(2, error.Line);
    }

    [Fact]
    public void Another_datum_is_rejected_with_its_name()
    {
        var error = Assert.Throws<DocumentLoadException>(() =>
            ReadText("OziExplorer Waypoint File Version 1.1\r\nPulkovo 1942\r\nReserved 2\r\ngarmin\r\n", "x.wpt"));

        Assert.Equal(DocumentLoadError.UnsupportedDatum, error.Error);
        Assert.Equal("Pulkovo 1942", error.Detail);
    }

    [Fact]
    public void OziExplorer_no_data_values_are_left_empty_and_ANSI_names_are_read()
    {
        var text = "OziExplorer Track Point File Version 2.1\r\nWGS 84\r\nAltitude is in Feet\r\nReserved 3\r\n"
            + "0,2,255,Peña Gorbea,0,0,2,8421376\r\n1\r\n43.1,-2.5,1,-777,0,,\r\n";

        var read = ReadText(text, "x.plt", Encoding.Latin1);

        var track = Assert.Single(read.Result.Data.Tracks);
        Assert.Equal("Peña Gorbea", track.Name);
        var point = Assert.Single(track.Segments[0].Points);
        Assert.Null(point.Elevation);
        Assert.Null(point.Time);
    }

    [Fact]
    public void OziExplorer_bad_coordinates_report_the_line()
    {
        var text = "OziExplorer Track Point File Version 2.1\nWGS 84\nAltitude is in Feet\nReserved 3\n0,2,255,,0\n1\n91.0,-2.5,0,0,0\n";

        var error = Assert.Throws<DocumentLoadException>(() => ReadText(text, "x.plt"));

        Assert.Equal(DocumentLoadError.InvalidCoordinate, error.Error);
        Assert.Equal(7, error.Line);
    }

    [Fact]
    public void The_new_extensions_can_be_opened()
    {
        Assert.Contains(".plt", TrailFileReader.Extensions);
        Assert.Contains(".wpt", TrailFileReader.Extensions);
        Assert.Contains(".trk", TrailFileReader.Extensions);
    }
}
