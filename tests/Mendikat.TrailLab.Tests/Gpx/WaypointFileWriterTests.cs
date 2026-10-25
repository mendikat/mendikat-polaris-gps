using System.Text;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Domain.Model;
using Mendikat.TrailLab.Gpx;
using Mendikat.TrailLab.Gpx.Wpt;

namespace Mendikat.TrailLab.Tests.Gpx;

public sealed class WaypointFileWriterTests
{
    private static TrailData Read(string fixture)
    {
        using var stream = Fixture.Open($"wpt/{fixture}");
        return new WaypointFileReader().Read(stream).Data;
    }

    private static TrailData ReadBytes(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return new WaypointFileReader().Read(stream).Data;
    }

    private static string[] Lines(byte[] bytes) =>
        WaypointFileReader.Windows1252.GetString(bytes).Split("\r\n");

    [Fact]
    [Trait("Req", "WPT-04")]
    [Trait("Acc", "ACC-WPT-01")]
    public void Unchanged_ozi_file_is_written_byte_for_byte()
    {
        var original = File.ReadAllBytes(Fixture.PathOf("wpt/ozi-sample.wpt"));

        var written = WaypointFileWriter.Write(Read("ozi-sample.wpt"));

        // The only difference allowed: the trailing blank line of the fixture (blank lines carry no data).
        Assert.Equal(original[..^2], written);
    }

    [Fact]
    [Trait("Req", "WPT-04")]
    [Trait("Req", "WPT-05")]
    public void Unchanged_compegps_ansi_file_is_written_byte_for_byte()
    {
        var original = File.ReadAllBytes(Fixture.PathOf("wpt/compegps-ansi.wpt"));

        Assert.Equal(original, WaypointFileWriter.Write(Read("compegps-ansi.wpt")));
    }

    [Fact]
    [Trait("Req", "WPT-04")]
    [Trait("Req", "WPT-05")]
    public void Utf8_file_stays_utf8_and_only_line_endings_become_crlf()
    {
        var original = File.ReadAllText(Fixture.PathOf("wpt/compegps-utf8.wpt"), Encoding.UTF8);

        var written = WaypointFileWriter.Write(Read("compegps-utf8.wpt"));

        Assert.Equal(original.Replace("\n", "\r\n"), new UTF8Encoding(false).GetString(written));
        Assert.False(written.AsSpan().StartsWith(Encoding.UTF8.Preamble)); // no BOM added
    }

    [Fact]
    [Trait("Req", "WPT-04")]
    [Trait("Acc", "ACC-WPT-02")]
    public void Editing_the_name_rewrites_only_that_field()
    {
        var data = Read("ozi-sample.wpt");
        data.Waypoints[0].Name = "PEÑA ALTA NORTE";

        var line = Lines(WaypointFileWriter.Write(data))[4];

        Assert.Equal("1,PEÑA ALTA NORTE,42.985300,-2.078600,46143.2916667,0,1,3,0,65535,Cumbre principal,0,0,0,4695.5,6,0,17", line);
    }

    [Fact]
    [Trait("Req", "WPT-03")]
    public void Ozi_elevation_time_and_position_edits_use_the_format_units()
    {
        var data = Read("ozi-sample.wpt");
        var point = data.Waypoints[1];
        point.Elevation = 100;
        point.Time = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
        point.Position = new GeoPosition(43.5, -2.25);

        var fields = Lines(WaypointFileWriter.Write(data))[5].Split(',');
        Assert.Equal("328.084", fields[14]);   // feet
        Assert.Equal("46143.5", fields[4]);    // Delphi day number
        Assert.Equal(("43.5", "-2.25"), (fields[2], fields[3]));

        point.Elevation = null;
        Assert.Equal("-777", Lines(WaypointFileWriter.Write(data))[5].Split(',')[14]);

        var copy = ReadBytes(WaypointFileWriter.Write(data)).Waypoints[1];
        Assert.Null(copy.Elevation);
        Assert.Equal(point.Time, copy.Time);
    }

    [Fact]
    [Trait("Req", "WPT-04")]
    public void Short_ozi_lines_are_completed_when_a_missing_field_is_edited()
    {
        var data = Read("ozi-sample.wpt");
        data.Waypoints[2].Description = "Fuente";

        var fields = Lines(WaypointFileWriter.Write(data))[6].Split(',');

        Assert.Equal(18, fields.Length);
        Assert.Equal("Fuente", fields[10]);
        Assert.Equal("Fuente", ReadBytes(WaypointFileWriter.Write(data)).Waypoints[2].Description);
    }

    [Fact]
    [Trait("Req", "WPE-04")]
    public void Deleted_waypoints_are_not_written()
    {
        var data = Read("ozi-sample.wpt");
        data.Waypoints.RemoveAt(0);

        var copy = ReadBytes(WaypointFileWriter.Write(data));

        Assert.Equal(["LARRAITZ", "FUENTE"], copy.Waypoints.Select(w => w.Name));
    }

    [Fact]
    [Trait("Req", "WPT-06")]
    public void Compegps_edit_rebuilds_the_waypoint_line_and_keeps_its_extra_lines()
    {
        var data = Read("compegps-ansi.wpt");
        data.Waypoints[0].Description = "Cima de Txindoki";
        data.Waypoints[0].Time = new DateTimeOffset(2026, 5, 1, 7, 30, 0, TimeSpan.Zero);

        var lines = Lines(WaypointFileWriter.Write(data));

        Assert.Equal("W  TXINDOKI A 42.9853000000ºN 2.0786000000ºW 01-MAY-2026 07:30:00 1431.000000 Cima de Txindoki", lines[2]);
        Assert.StartsWith("w Waypoint", lines[3]);
        var copy = ReadBytes(WaypointFileWriter.Write(data)).Waypoints[0];
        Assert.Equal(("Cima de Txindoki", data.Waypoints[0].Time), (copy.Description, copy.Time));
    }

    [Fact]
    [Trait("Req", "WPE-04")]
    public void Characters_the_encoding_cannot_store_are_reported_and_replaced()
    {
        var data = Read("ozi-sample.wpt");
        data.Waypoints[0].Name = "Пик"; // Cyrillic: not in Windows-1252

        var issue = Assert.Single(WaypointFileWriter.Check(data));

        Assert.Equal(new SaveIssue(SaveIssueKind.UnrepresentableCharacters, 3), issue);
        Assert.StartsWith("1,???,", Lines(WaypointFileWriter.Write(data))[4]);
    }

    [Fact]
    [Trait("Req", "WPE-04")]
    public void Compegps_waypoints_without_elevation_are_reported()
    {
        var data = Read("compegps-ansi.wpt");
        data.Waypoints[1].Elevation = null;

        Assert.Equal(new SaveIssue(SaveIssueKind.MissingElevationWrittenAsZero, 1), Assert.Single(WaypointFileWriter.Check(data)));
    }
}
