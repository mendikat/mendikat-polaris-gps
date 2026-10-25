using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Model;

namespace Mendikat.TrailLab.Gpx.Wpt;

/// <summary>
/// CompeGPS / TwoNav waypoint file (ADR-0006). Each line starts with a type letter:
/// G datum, U coordinate system (only 1 = lat/lon supported), W waypoint, w extra waypoint info, others kept.
/// Waypoint line: <c>W  name A 43.1234ºN 1.2345ºW 27-MAR-62 00:00:00 123.0 description…</c>.
/// </summary>
public sealed partial class CompeGpsFormat : IWaypointFileFormat
{
    /// <summary>Fixed date CompeGPS writes when the waypoint has no date.</summary>
    public const string NoDate = "27-MAR-62";

    private static readonly string[] DateFormats = ["dd-MMM-yy HH:mm:ss", "dd-MMM-yyyy HH:mm:ss"];

    public string DialectId => "compegps";

    public bool CanRead(IReadOnlyList<string> lines)
    {
        var first = lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
        return first is not null && LineType(first) == 'G';
    }

    public TrailFileResult Read(IReadOnlyList<string> lines, Encoding encoding)
    {
        var header = new List<string>();
        var data = new TrailData();
        var timesWithoutZone = 0;
        List<string>? currentLines = null;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var lineNumber = i + 1;
            switch (LineType(line))
            {
                case 'G':
                    CheckDatum(line, lineNumber);
                    break;
                case 'U':
                    CheckCoordinateSystem(line, lineNumber);
                    break;
                case 'W':
                    currentLines = [line];
                    var point = ReadWaypoint(line, lineNumber, currentLines);
                    if (point.Time is not null)
                    {
                        timesWithoutZone++;
                    }

                    data.Waypoints.Add(point);
                    continue;
            }

            // Header lines before the first waypoint; afterwards, extra lines of the current waypoint ("w", blank…).
            (currentLines ?? header).Add(line);
        }

        data.FormatState = new WptFormatState { DialectId = DialectId, Encoding = encoding, HeaderLines = header };
        var warnings = new List<LoadWarning>();
        if (timesWithoutZone > 0)
        {
            warnings.Add(new LoadWarning(LoadWarningKind.TimesWithoutZone, timesWithoutZone));
        }

        if (data.IsEmpty)
        {
            warnings.Add(new LoadWarning(LoadWarningKind.NoData));
        }

        return new TrailFileResult(data, warnings);
    }

    private static char LineType(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.Length >= 2 && char.IsWhiteSpace(trimmed[1]) ? trimmed[0] : '\0';
    }

    private static string Value(string line) => line.TrimStart()[1..].Trim();

    private static void CheckDatum(string line, int lineNumber)
    {
        var datum = Value(line);
        if (!WptParsing.IsWgs84(datum))
        {
            throw new DocumentLoadException(DocumentLoadError.UnsupportedDatum, datum, lineNumber);
        }
    }

    private static void CheckCoordinateSystem(string line, int lineNumber)
    {
        var system = Value(line);
        if (system != "1")
        {
            throw new DocumentLoadException(DocumentLoadError.UnsupportedCoordinateSystem, $"U {system}", lineNumber);
        }
    }

    private static TrailPoint ReadWaypoint(string line, int lineNumber, List<string> pointLines)
    {
        // Tokens: W, name, A, lat, lon, date, time, altitude, description words…
        var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 5)
        {
            throw new DocumentLoadException(DocumentLoadError.InvalidCoordinate, "missing fields", lineNumber);
        }

        if (tokens[2] != "A")
        {
            throw new DocumentLoadException(DocumentLoadError.UnsupportedCoordinateSystem, tokens[2], lineNumber);
        }

        var lat = ParseCoordinate(tokens[3], 'N', 'S', lineNumber);
        var lon = ParseCoordinate(tokens[4], 'E', 'W', lineNumber);
        return WptParsing.RememberOriginal(new TrailPoint(WptParsing.ToPosition(lat, lon, lineNumber))
        {
            Name = tokens[1],
            Time = tokens.Length > 6 ? ParseDate(tokens[5], tokens[6], lineNumber) : null,
            Elevation = tokens.Length > 7 ? WptParsing.ParseDouble(tokens[7], lineNumber, DocumentLoadError.InvalidNumber) : null,
            Description = tokens.Length > 8 ? string.Join(' ', tokens.Skip(8)) : null,
            FormatState = new WptPointState { Lines = pointLines, Fields = tokens },
        });
    }

    /// <summary>"46.0294899313ºN": unsigned decimal degrees, optional degree sign, hemisphere letter.</summary>
    private static double ParseCoordinate(string token, char positive, char negative, int lineNumber)
    {
        var match = CoordinatePattern().Match(token);
        if (!match.Success || !WptParsing.TryParseDouble(match.Groups["value"].Value, out var value))
        {
            throw new DocumentLoadException(DocumentLoadError.InvalidCoordinate, token, lineNumber);
        }

        var hemisphere = char.ToUpperInvariant(match.Groups["hemi"].Value[0]);
        if (hemisphere != positive && hemisphere != negative)
        {
            throw new DocumentLoadException(DocumentLoadError.InvalidCoordinate, token, lineNumber);
        }

        return hemisphere == negative ? -value : value;
    }

    private static DateTimeOffset? ParseDate(string date, string time, int lineNumber)
    {
        if (date.Equals(NoDate, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return DateTime.TryParseExact(
                $"{date} {time}", DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value)
            ? new DateTimeOffset(value, TimeSpan.Zero)
            : throw new DocumentLoadException(DocumentLoadError.InvalidDate, $"{date} {time}", lineNumber);
    }

    [GeneratedRegex(@"^(?<value>\d+(\.\d+)?)[^\dNSEWnsew]*(?<hemi>[NSEWnsew])$")]
    private static partial Regex CoordinatePattern();
}
