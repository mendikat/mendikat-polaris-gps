using System.Globalization;
using Mendikat.TrailLab.Domain.Editing;
using Mendikat.TrailLab.Domain.Model;

namespace Mendikat.TrailLab.Gpx.Wpt;

/// <summary>
/// Writes the waypoint lines of both WPT dialects (WPT-04, ADR-0006). A waypoint whose values were not edited is
/// written with its original lines, byte for byte; otherwise only the edited fields are replaced.
/// </summary>
internal static class WaypointLineWriter
{
    private static readonly DateTime DelphiEpoch = new(1899, 12, 30, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Default fields of a new OziExplorer waypoint line (as written by OziExplorer).</summary>
    private static readonly string[] OziDefaults =
        ["-1", "", "0", "0", "", "0", "1", "3", "0", "65535", "", "0", "0", "0", "-777", "6", "0", "17"];

    public static IEnumerable<string> OziExplorer(TrailPoint point, int number)
    {
        var state = point.FormatState as WptPointState;
        var current = PointValues.From(point);
        if (state?.Original is { } original && original == current)
        {
            return state.Lines;
        }

        var fields = (state?.Fields ?? OziDefaults).ToList();
        while (fields.Count < OziDefaults.Length)
        {
            fields.Add(OziDefaults[fields.Count]);
        }

        if (state is null)
        {
            fields[0] = number.ToString(CultureInfo.InvariantCulture);
        }

        var before = state?.Original;
        if (before?.Name != current.Name)
        {
            fields[OziExplorerFormat.NameField] = current.Name ?? string.Empty;
        }

        if (before?.Position != current.Position)
        {
            fields[OziExplorerFormat.LatitudeField] = Coordinate(current.Position.Latitude);
            fields[OziExplorerFormat.LongitudeField] = Coordinate(current.Position.Longitude);
        }

        if (before?.Time != current.Time)
        {
            fields[OziExplorerFormat.DateField] = current.Time is { } t
                ? (t.UtcDateTime - DelphiEpoch).TotalDays.ToString("0.#######", CultureInfo.InvariantCulture)
                : string.Empty;
        }

        if (before?.Description != current.Description)
        {
            fields[OziExplorerFormat.DescriptionField] = current.Description ?? string.Empty;
        }

        if (before?.Elevation != current.Elevation)
        {
            fields[OziExplorerFormat.AltitudeFeetField] = current.Elevation is { } meters
                ? (meters / OziExplorerFormat.FeetToMeters).ToString("0.###", CultureInfo.InvariantCulture)
                : OziExplorerFormat.NoAltitude.ToString(CultureInfo.InvariantCulture);
        }

        return [string.Join(',', fields)];
    }

    /// <summary>CompeGPS has no "no elevation": a missing one is written as 0 (reported as a save issue).</summary>
    public static IEnumerable<string> CompeGps(TrailPoint point)
    {
        var state = point.FormatState as WptPointState;
        var current = PointValues.From(point);
        if (state?.Original is { } original && original == current)
        {
            return state.Lines;
        }

        var lat = current.Position.Latitude;
        var lon = current.Position.Longitude;
        var line = string.Create(CultureInfo.InvariantCulture,
            $"W  {current.Name} A {Math.Abs(lat):0.0000000000}º{(lat < 0 ? 'S' : 'N')} {Math.Abs(lon):0.0000000000}º{(lon < 0 ? 'W' : 'E')} "
            + $"{CompeGpsDate(current.Time)} {current.Elevation ?? 0:0.000000}");
        if (!string.IsNullOrEmpty(current.Description))
        {
            line += " " + current.Description;
        }

        // Extra lines of the waypoint ("w" info, blank lines) are kept.
        return state is null ? [line] : [line, .. state.Lines.Skip(1)];
    }

    private static string CompeGpsDate(DateTimeOffset? time) =>
        time is { } t
            ? t.UtcDateTime.ToString("dd-MMM-yyyy HH:mm:ss", CultureInfo.InvariantCulture).ToUpperInvariant()
            : CompeGpsFormat.NoDate + " 00:00:00";

    private static string Coordinate(double degrees) => degrees.ToString("0.0######", CultureInfo.InvariantCulture);
}
