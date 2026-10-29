using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Model;

namespace Mendikat.TrailLab.Gpx;

/// <summary>
/// Reads OziExplorer track (.plt) and waypoint (.wpt) files and converts them to GPX data, as mendikat.net does
/// (<c>lib/GPS/Oziexplorer</c>): a track file becomes one track with one segment, a waypoint file the waypoints of the
/// document. Elevations are in feet; ED50 coordinates are converted to WGS84. Differences with mendikat.net: dates
/// count from midnight of 30/12/1899 (the Delphi epoch; the PHP counts from noon), and the "no data" values
/// (elevation −777, date 0) are left empty instead of being read as numbers.
/// </summary>
public sealed class OziExplorerReader
{
    private const string TrackHeader = "OziExplorer Track Point";
    private const string WaypointHeader = "OziExplorer Waypoint";
    private const double FeetToMeters = 0.3048;
    private const double NoElevation = -777;
    private static readonly DateTime DelphiEpoch = new(1899, 12, 30, 0, 0, 0, DateTimeKind.Utc);

    private int _timesWithoutZone;

    /// <summary>True if the text starts like an OziExplorer track or waypoint file.</summary>
    public static bool IsOziExplorer(string head) =>
        head.StartsWith(TrackHeader, StringComparison.OrdinalIgnoreCase)
        || head.StartsWith(WaypointHeader, StringComparison.OrdinalIgnoreCase);

    /// <exception cref="DocumentLoadException">Not a valid OziExplorer track or waypoint file.</exception>
    /// <exception cref="OperationCanceledException">Cancelled.</exception>
    public TrailFileResult Read(Stream stream, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var report = new LoadProgress(progress, cancellationToken);
        report.Report(0);
        var lines = LegacyText.Lines(LegacyText.ReadAll(stream));
        _timesWithoutZone = 0;
        var data = ConvertedData.Create(SourceFormat.OziExplorer, null, null);
        var header = lines[0].Trim();
        if (header.StartsWith(TrackHeader, StringComparison.OrdinalIgnoreCase))
        {
            ReadTrack(lines, data, report);
        }
        else if (header.StartsWith(WaypointHeader, StringComparison.OrdinalIgnoreCase))
        {
            ReadWaypoints(lines, data, report);
        }
        else
        {
            throw new DocumentLoadException(DocumentLoadError.UnsupportedFormat, header.Length > 40 ? header[..40] : header);
        }

        report.Report(1);
        return new TrailFileResult(data, ConvertedData.Warnings(SourceFormat.OziExplorer, data, _timesWithoutZone, []));
    }

    /// <summary>Lines: header, datum, altitude unit, reserved, track properties (name in field 4), point count, points.</summary>
    private void ReadTrack(string[] lines, TrailData data, LoadProgress report)
    {
        var datum = LegacyText.ParseDatum(Line(lines, 1), 2);
        var properties = Line(lines, 4).Split(',');
        var segment = new TrackSegment();
        for (var i = 6; i < lines.Length; i++)
        {
            report.Report((double)i / lines.Length);
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            var fields = lines[i].Split(',');
            var line = i + 1;
            var point = new TrailPoint(LegacyText.Position(
                LegacyText.Number(Field(fields, 0), DocumentLoadError.InvalidCoordinate, line),
                LegacyText.Number(Field(fields, 1), DocumentLoadError.InvalidCoordinate, line),
                datum, line))
            {
                Elevation = Elevation(Field(fields, 3), line),
                Time = Time(Field(fields, 4), line),
            };
            segment.Points.Add(point);
        }

        if (segment.Points.Count > 0)
        {
            var track = new Track { Name = LegacyText.Text(Field(properties, 3)) };
            track.Segments.Add(segment);
            data.Tracks.Add(track);
        }
    }

    /// <summary>Lines: header, datum, reserved, symbol set, waypoints (number, name, latitude, longitude, date…).</summary>
    private void ReadWaypoints(string[] lines, TrailData data, LoadProgress report)
    {
        var datum = LegacyText.ParseDatum(Line(lines, 1), 2);
        for (var i = 4; i < lines.Length; i++)
        {
            report.Report((double)i / lines.Length);
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            var fields = lines[i].Split(',');
            var line = i + 1;
            data.Waypoints.Add(new TrailPoint(LegacyText.Position(
                LegacyText.Number(Field(fields, 2), DocumentLoadError.InvalidCoordinate, line),
                LegacyText.Number(Field(fields, 3), DocumentLoadError.InvalidCoordinate, line),
                datum, line))
            {
                Name = LegacyText.Text(Field(fields, 1)),
                Description = LegacyText.Text(Field(fields, 10)),
                Elevation = Elevation(Field(fields, 14), line),
                Time = Time(Field(fields, 4), line),
            });
        }
    }

    private static string Line(string[] lines, int index) => index < lines.Length ? lines[index] : string.Empty;

    private static string? Field(string[] fields, int index) => index < fields.Length ? fields[index] : null;

    /// <summary>Feet to meters; empty or −777 means no elevation.</summary>
    private static double? Elevation(string? text, int line)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var feet = LegacyText.Number(text, DocumentLoadError.InvalidNumber, line);
        return feet == NoElevation ? null : feet * FeetToMeters;
    }

    /// <summary>
    /// Delphi TDateTime: whole days since 30/12/1899 plus the fraction of the day, truncated to seconds as mendikat.net
    /// does. OziExplorer stores no time zone: read as UTC. Empty or 0 means no time.
    /// </summary>
    private DateTimeOffset? Time(string? text, int line)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var value = LegacyText.Number(text, DocumentLoadError.InvalidDate, line);
        if (value <= 0 || value > 2_958_465) // 31/12/9999
        {
            return null;
        }

        var days = Math.Truncate(value);
        var seconds = Math.Floor((value - days) * 86400);
        _timesWithoutZone++;
        return new DateTimeOffset(DelphiEpoch.AddDays(days).AddSeconds(seconds));
    }
}
