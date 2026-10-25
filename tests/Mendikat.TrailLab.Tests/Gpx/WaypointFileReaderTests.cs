using System.Text;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Gpx;
using Mendikat.TrailLab.Gpx.Wpt;

namespace Mendikat.TrailLab.Tests.Gpx;

public sealed class WaypointFileReaderTests
{
    private static TrailFileResult Read(string fixture)
    {
        using var stream = Fixture.Open($"wpt/{fixture}");
        return new WaypointFileReader().Read(stream);
    }

    private static DocumentLoadException ReadFails(string fixture) =>
        Assert.Throws<DocumentLoadException>(() => Read(fixture));

    [Fact]
    [Trait("Req", "WPT-01")]
    [Trait("Acc", "ACC-WPT-01")]
    public void OziExplorer_file_is_read_with_names_coordinates_and_descriptions()
    {
        var data = Read("ozi-sample.wpt").Data;

        Assert.Equal(3, data.Waypoints.Count);
        var first = data.Waypoints[0];
        Assert.Equal(42.9853, first.Position.Latitude);
        Assert.Equal(-2.0786, first.Position.Longitude);
        Assert.Equal("Cumbre principal", first.Description);
        Assert.Null(data.Waypoints[2].Description); // line with only 4 fields
        var state = Assert.IsType<WptFormatState>(data.FormatState);
        Assert.Equal("ozi", state.DialectId);
        Assert.Equal(4, state.HeaderLines.Count);
    }

    [Fact]
    [Trait("Req", "WPT-05")]
    public void OziExplorer_ansi_enye_is_kept_and_not_converted_to_a_comma()
    {
        var data = Read("ozi-sample.wpt").Data;

        Assert.Equal("PEÑA ALTA", data.Waypoints[0].Name);
        Assert.Equal(1252, Assert.IsType<WptFormatState>(data.FormatState).Encoding.CodePage);
    }

    [Fact]
    [Trait("Req", "WPT-03")]
    public void OziExplorer_altitude_in_feet_becomes_meters_and_minus_777_means_none()
    {
        var waypoints = Read("ozi-sample.wpt").Data.Waypoints;

        Assert.Equal(4695.5 * 0.3048, waypoints[0].Elevation!.Value, precision: 9);
        Assert.Null(waypoints[1].Elevation);
        Assert.Null(waypoints[2].Elevation);
    }

    [Fact]
    [Trait("Req", "WPT-01")]
    public void OziExplorer_date_is_a_delphi_day_number_read_as_utc_with_a_warning()
    {
        var result = Read("ozi-sample.wpt");

        Assert.Equal(new DateTimeOffset(2026, 5, 1, 7, 0, 0, TimeSpan.Zero), result.Data.Waypoints[0].Time);
        Assert.Null(result.Data.Waypoints[1].Time);
        Assert.Contains(new LoadWarning(LoadWarningKind.TimesWithoutZone, 1), result.Warnings);
    }

    [Fact]
    [Trait("Req", "WPT-04")]
    public void OziExplorer_original_lines_are_kept_for_saving()
    {
        var point = Read("ozi-sample.wpt").Data.Waypoints[1];

        var state = Assert.IsType<WptPointState>(point.FormatState);
        Assert.Equal(["2,LARRAITZ,42.992100,-2.061000,,0,1,3,0,65535,Aparcamiento,0,0,0,-777,6,0,17"], state.Lines);
        Assert.Equal(18, state.Fields.Count);
    }

    [Fact]
    [Trait("Req", "WPT-02")]
    public void OziExplorer_other_datum_is_rejected_naming_the_datum()
    {
        var ex = ReadFails("ozi-ed50.wpt");

        Assert.Equal(DocumentLoadError.UnsupportedDatum, ex.Error);
        Assert.Equal("European 1950", ex.Detail);
        Assert.Equal(2, ex.Line);
    }

    [Fact]
    [Trait("Req", "WPT-02")]
    public void OziExplorer_longitude_out_of_range_is_rejected_with_line()
    {
        var ex = ReadFails("ozi-bad-lon.wpt");

        Assert.Equal(DocumentLoadError.InvalidCoordinate, ex.Error);
        Assert.Equal(5, ex.Line);
    }

    [Fact]
    [Trait("Req", "WPT-06")]
    public void CompeGps_ansi_file_is_read()
    {
        var data = Read("compegps-ansi.wpt").Data;

        Assert.Equal(2, data.Waypoints.Count);
        var txindoki = data.Waypoints[0];
        Assert.Equal("TXINDOKI", txindoki.Name);
        Assert.Equal(42.9853, txindoki.Position.Latitude, precision: 10);
        Assert.Equal(-2.0786, txindoki.Position.Longitude, precision: 10);
        Assert.Equal(1431, txindoki.Elevation);
        Assert.Equal("Cumbre de Txindoki", txindoki.Description);
        Assert.Null(txindoki.Time); // 27-MAR-62 means "no date"

        var larraitz = data.Waypoints[1];
        Assert.Equal(new DateTimeOffset(2026, 5, 1, 7, 30, 15, TimeSpan.Zero), larraitz.Time);
        Assert.Equal(512.5, larraitz.Elevation);
        Assert.Null(larraitz.Description);
    }

    [Fact]
    [Trait("Req", "WPT-06")]
    public void CompeGps_keeps_header_and_extra_waypoint_lines()
    {
        var data = Read("compegps-ansi.wpt").Data;

        var state = Assert.IsType<WptFormatState>(data.FormatState);
        Assert.Equal("compegps", state.DialectId);
        Assert.Equal(["G  WGS 84", "U  1"], state.HeaderLines);
        var point = Assert.IsType<WptPointState>(data.Waypoints[0].FormatState);
        Assert.Equal(2, point.Lines.Count);
        Assert.StartsWith("w Waypoint", point.Lines[1]);
    }

    [Fact]
    [Trait("Req", "WPT-05")]
    [Trait("Req", "WPT-06")]
    public void CompeGps_utf8_file_with_blank_lines_is_read()
    {
        var data = Read("compegps-utf8.wpt").Data;

        var point = Assert.Single(data.Waypoints);
        Assert.Equal("wpt001", point.Name);
        Assert.Equal(41.80711816, point.Position.Latitude, precision: 10);
        Assert.Equal(2.34416887, point.Position.Longitude, precision: 10);
        Assert.Equal(new DateTimeOffset(2010, 5, 16, 9, 30, 3, TimeSpan.Zero), point.Time);
        Assert.Equal(Encoding.UTF8.CodePage, Assert.IsType<WptFormatState>(data.FormatState).Encoding.CodePage);
    }

    [Theory]
    [Trait("Req", "WPT-06")]
    [Trait("Req", "WPT-07")]
    [InlineData("compegps-utm.wpt", DocumentLoadError.UnsupportedCoordinateSystem)]
    [InlineData("compegps-bad-hemisphere.wpt", DocumentLoadError.InvalidCoordinate)]
    [InlineData("unknown.wpt", DocumentLoadError.UnsupportedWptDialect)]
    public void Unsupported_or_invalid_wpt_files_are_rejected(string fixture, DocumentLoadError expected)
    {
        Assert.Equal(expected, ReadFails(fixture).Error);
    }

    [Theory]
    [Trait("Req", "WPT-05")]
    [InlineData(new byte[] { 0x41, 0x42 }, 1252)]                 // ASCII
    [InlineData(new byte[] { 0x50, 0x45, 0xD1, 0x41 }, 1252)]     // "PEÑA" in Windows-1252 (invalid UTF-8)
    [InlineData(new byte[] { 0x50, 0x45, 0xC3, 0x91, 0x41 }, 65001)] // "PEÑA" in UTF-8
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF, 0x41 }, 65001)]    // UTF-8 BOM
    public void Encoding_is_detected_from_content(byte[] bytes, int expectedCodePage)
    {
        var (_, encoding) = WaypointFileReader.Decode(bytes);

        Assert.Equal(expectedCodePage, encoding.CodePage);
    }
}
