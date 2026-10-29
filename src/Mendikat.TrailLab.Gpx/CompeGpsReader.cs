using System.Globalization;
using System.Text.RegularExpressions;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Model;

namespace Mendikat.TrailLab.Gpx;

/// <summary>
/// Reads CompeGPS/TwoNav track (.trk) and waypoint (.wpt) files and converts them to GPX data, as mendikat.net does
/// (<c>lib/GPS/CompeGPS</c>). Each line starts with its type: G datum, U coordinate system, L time zone, N track name,
/// T track point (one track with one segment), W waypoint and w its symbol. ED50 coordinates are converted to WGS84 and
/// times to UTC with the L offset; dates before 1999 or "NUL" mean no time. Only geographic coordinates (U 1) are read.
/// Differences with mendikat.net: the waypoint description keeps all its words (the PHP keeps the first one), the
/// symbol is kept, and the time zone keeps its minutes.
/// </summary>
public sealed partial class CompeGpsReader
{
    private int _timesWithoutZone;

    /// <summary>True if the text starts like a CompeGPS/TwoNav file: a "B" (encoding) or "G" (datum) line.</summary>
    public static bool IsCompeGps(string head) => HeaderLine().IsMatch(head);

    /// <exception cref="DocumentLoadException">Not a valid CompeGPS/TwoNav file.</exception>
    /// <exception cref="OperationCanceledException">Cancelled.</exception>
    public TrailFileResult Read(Stream stream, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var report = new LoadProgress(progress, cancellationToken);
        report.Report(0);
        var lines = LegacyText.Lines(LegacyText.ReadAll(stream));
        if (!IsCompeGps(lines[0].TrimStart('﻿')))
        {
            throw new DocumentLoadException(DocumentLoadError.UnsupportedFormat, lines[0].Length > 40 ? lines[0][..40] : lines[0]);
        }

        _timesWithoutZone = 0;
        var data = ConvertedData.Create(SourceFormat.CompeGps, null, null);
        var datum = LegacyDatum.Wgs84;
        TimeSpan? offset = null;
        string? trackName = null;
        var segment = new TrackSegment();
        TrailPoint? lastWaypoint = null;
        for (var i = 0; i < lines.Length; i++)
        {
            report.Report((double)i / lines.Length);
            var text = lines[i].TrimStart('﻿');
            if (text.Length == 0)
            {
                continue;
            }

            var line = i + 1;
            var rest = text.Length > 1 ? text[1..] : string.Empty;
            switch (text[0])
            {
                case 'G':
                    datum = LegacyText.ParseDatum(rest, line);
                    break;
                case 'U' when rest.Trim() != "1":
                    throw new DocumentLoadException(DocumentLoadError.UnsupportedDatum, "UTM", line);
                case 'L':
                    offset = ParseOffset(rest, line);
                    break;
                case 'N' when trackName is null:
                    trackName = LegacyText.Text(rest);
                    break;
                case 'T':
                    segment.Points.Add(ReadTrackPoint(Tokens(rest), datum, offset, line));
                    break;
                case 'W':
                    lastWaypoint = ReadWaypoint(Tokens(rest), datum, offset, line);
                    data.Waypoints.Add(lastWaypoint);
                    break;
                case 'w' when lastWaypoint is not null:
                    lastWaypoint.Symbol ??= LegacyText.Text(rest.Split(',')[0]);
                    break;
            }
        }

        if (segment.Points.Count > 0)
        {
            var track = new Track { Name = trackName };
            track.Segments.Add(segment);
            data.Tracks.Insert(0, track);
        }

        report.Report(1);
        return new TrailFileResult(data, ConvertedData.Warnings(SourceFormat.CompeGps, data, _timesWithoutZone, []));
    }

    /// <summary>"A latitude longitude date time type elevation …".</summary>
    private TrailPoint ReadTrackPoint(string[] tokens, LegacyDatum datum, TimeSpan? offset, int line) =>
        new(LegacyText.Position(Coordinate(Token(tokens, 1), 'N', line), Coordinate(Token(tokens, 2), 'E', line), datum, line))
        {
            Time = Time(Token(tokens, 3), Token(tokens, 4), offset, line),
            Elevation = Elevation(Token(tokens, 6), line),
        };

    /// <summary>"name A latitude longitude date time elevation description…".</summary>
    private TrailPoint ReadWaypoint(string[] tokens, LegacyDatum datum, TimeSpan? offset, int line) =>
        new(LegacyText.Position(Coordinate(Token(tokens, 2), 'N', line), Coordinate(Token(tokens, 3), 'E', line), datum, line))
        {
            Name = Token(tokens, 0),
            Time = Time(Token(tokens, 4), Token(tokens, 5), offset, line),
            Elevation = Elevation(Token(tokens, 6), line),
            Description = tokens.Length > 7 ? LegacyText.Text(string.Join(' ', tokens[7..])) : null,
        };

    private static string[] Tokens(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static string? Token(string[] tokens, int index) => index < tokens.Length ? tokens[index] : null;

    /// <summary>"42.8800930694ºN": the digits, positive with the given hemisphere letter (N or E), as mendikat.net.</summary>
    private static double Coordinate(string? text, char positive, int line)
    {
        if (text is null)
        {
            throw new DocumentLoadException(DocumentLoadError.InvalidCoordinate, line: line);
        }

        var digits = NotNumber().Replace(text, string.Empty).Replace(',', '.');
        var value = LegacyText.Number(digits, DocumentLoadError.InvalidCoordinate, line);
        return text.Contains(positive, StringComparison.OrdinalIgnoreCase) ? value : -value;
    }

    private static double? Elevation(string? text, int line) =>
        string.IsNullOrWhiteSpace(text) ? null : LegacyText.Number(text, DocumentLoadError.InvalidNumber, line);

    /// <summary>"+02:00:00": local time minus this offset is UTC.</summary>
    private static TimeSpan ParseOffset(string text, int line)
    {
        var value = text.Trim();
        var negative = value.StartsWith('-');
        return TimeSpan.TryParse(value.TrimStart('+', '-'), CultureInfo.InvariantCulture, out var span)
            ? (negative ? -span : span)
            : throw new DocumentLoadException(DocumentLoadError.InvalidDate, value, line);
    }

    /// <summary>"12-NOV-15 14:30:00" or "02-JAN-2003 08:07:37.000"; "NUL" dates and dates before 1999 mean no time.</summary>
    private DateTimeOffset? Time(string? date, string? time, TimeSpan? offset, int line)
    {
        if (date is null || date.Contains("NUL", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var clock = time is null ? "00:00:00" : time.Split('.')[0];
        var format = date.Length == 9 ? "dd-MMM-yy HH:mm:ss" : "dd-MMM-yyyy HH:mm:ss";
        if (!DateTime.TryParseExact($"{date} {clock}", format, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var value))
        {
            throw new DocumentLoadException(DocumentLoadError.InvalidDate, $"{date} {time}".Trim(), line);
        }

        if (value.Year < 1999)
        {
            return null;
        }

        if (offset is null)
        {
            _timesWithoutZone++;
        }

        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc) - (offset ?? TimeSpan.Zero));
    }

    [GeneratedRegex(@"^[BG]  ")]
    private static partial Regex HeaderLine();

    [GeneratedRegex(@"[^0-9,.]")]
    private static partial Regex NotNumber();
}
