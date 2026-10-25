using System.Text;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Model;

namespace Mendikat.TrailLab.Gpx.Wpt;

/// <summary>
/// OziExplorer Waypoint File v1.1 (ADR-0006). Header: 4 lines (signature, datum, 2 reserved).
/// Each following line is a comma-separated waypoint; only the fields below are interpreted.
/// </summary>
public sealed class OziExplorerFormat : IWaypointFileFormat
{
    public const string Signature = "OziExplorer Waypoint File";
    public const int HeaderLineCount = 4;
    public const double FeetToMeters = 0.3048;
    public const double NoAltitude = -777;

    // Field positions (0-based).
    public const int NameField = 1;
    public const int LatitudeField = 2;
    public const int LongitudeField = 3;
    public const int DateField = 4;
    public const int DescriptionField = 10;
    public const int AltitudeFeetField = 14;

    /// <summary>Delphi TDateTime epoch: day 0 is 1899-12-30.</summary>
    private static readonly DateTime DelphiEpoch = new(1899, 12, 30, 0, 0, 0, DateTimeKind.Utc);

    public string DialectId => "ozi";

    public bool CanRead(IReadOnlyList<string> lines) =>
        lines.Count > 0 && lines[0].TrimStart().StartsWith(Signature, StringComparison.OrdinalIgnoreCase);

    public TrailFileResult Read(IReadOnlyList<string> lines, Encoding encoding)
    {
        var datum = lines.Count > 1 ? lines[1].Trim() : string.Empty;
        if (!WptParsing.IsWgs84(datum))
        {
            throw new DocumentLoadException(DocumentLoadError.UnsupportedDatum, datum, 2);
        }

        var headerCount = Math.Min(HeaderLineCount, lines.Count);
        var data = new TrailData
        {
            FormatState = new WptFormatState { DialectId = DialectId, Encoding = encoding, HeaderLines = lines.Take(headerCount).ToList() },
        };

        var timesWithoutZone = 0;
        for (var i = headerCount; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue; // blank lines (usually trailing) carry no data and are not written back
            }

            var point = ReadWaypoint(lines[i], lineNumber: i + 1);
            if (point.Time is not null)
            {
                timesWithoutZone++;
            }

            data.Waypoints.Add(point);
        }

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

    private static TrailPoint ReadWaypoint(string line, int lineNumber)
    {
        var fields = line.Split(',');
        if (fields.Length <= LongitudeField)
        {
            throw new DocumentLoadException(DocumentLoadError.InvalidCoordinate, "missing fields", lineNumber);
        }

        var lat = WptParsing.ParseDouble(fields[LatitudeField], lineNumber, DocumentLoadError.InvalidCoordinate);
        var lon = WptParsing.ParseDouble(fields[LongitudeField], lineNumber, DocumentLoadError.InvalidCoordinate);

        // Character 209 is kept as is: in Windows-1252 it is "Ñ" (ADR-0006).
        return WptParsing.RememberOriginal(new TrailPoint(WptParsing.ToPosition(lat, lon, lineNumber))
        {
            Name = WptParsing.NullIfBlank(fields[NameField]),
            Description = WptParsing.NullIfBlank(Field(fields, DescriptionField)),
            Time = ParseDate(Field(fields, DateField), lineNumber),
            Elevation = ParseAltitude(Field(fields, AltitudeFeetField), lineNumber),
            FormatState = new WptPointState { Lines = [line], Fields = fields },
        });
    }

    private static string Field(string[] fields, int index) => index < fields.Length ? fields[index] : string.Empty;

    /// <summary>Days since 1899-12-30 (fractional). Empty or 0 means no date. No time zone is defined: read as UTC.</summary>
    public static DateTimeOffset? ParseDate(string text, int lineNumber)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var days = WptParsing.ParseDouble(text, lineNumber, DocumentLoadError.InvalidDate);
        if (days == 0)
        {
            return null;
        }

        if (days is < -657434 or > 2958465) // DateTime range
        {
            throw new DocumentLoadException(DocumentLoadError.InvalidDate, text.Trim(), lineNumber);
        }

        var ticks = (long)Math.Round(days * TimeSpan.TicksPerDay / TimeSpan.TicksPerSecond) * TimeSpan.TicksPerSecond;
        return new DateTimeOffset(DelphiEpoch.AddTicks(ticks));
    }

    /// <summary>Altitude in feet → meters. Empty or -777 means no altitude.</summary>
    public static double? ParseAltitude(string text, int lineNumber)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var feet = WptParsing.ParseDouble(text, lineNumber, DocumentLoadError.InvalidNumber);
        return feet == NoAltitude ? null : feet * FeetToMeters;
    }
}
