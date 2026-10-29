using System.Globalization;
using System.Text;

namespace Mendikat.TrailLab.Mapping.Local;

/// <summary>
/// Reads CompeGPS/TwoNav calibration files (.imp, "CompeGPS MAP File"; LMAP-06). The .imp does not hold the image:
/// "Bitmap=" in the &lt;Map&gt; section names it. &lt;Calibration&gt; holds "Pn=pixel x,pixel y,zone,X,Y" where zone is
/// "A" for longitude/latitude or a UTM zone such as "30T" for easting/northing. "Datum=" names the datum and
/// "Projection=" must be UTM or Latitude/Longitude.
/// </summary>
public static class CompeImpReader
{
    private const string Signature = "CompeGPS MAP File";

    /// <exception cref="LocalMapException">Not a valid calibration, or its image is missing.</exception>
    public static MapCalibration Read(string path)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(path, Encoding.Latin1);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new LocalMapException(LocalMapError.ReadFailed, Path.GetFileName(path), innerException: ex);
        }

        if (lines.Length == 0 || !lines[0].Trim().StartsWith(Signature, StringComparison.OrdinalIgnoreCase))
        {
            throw new LocalMapException(LocalMapError.InvalidCalibration, "no es un archivo .imp de CompeGPS", 1);
        }

        var values = KeyValues(lines);
        var bitmap = values.FirstOrDefault(v => v.Key.Equals("Bitmap", StringComparison.OrdinalIgnoreCase)).Value
            ?? throw new LocalMapException(LocalMapError.InvalidCalibration, "falta «Bitmap=»");
        return Parse(values, LocalFiles.ResolveImage(path, bitmap), Path.GetFileNameWithoutExtension(path));
    }

    /// <summary>
    /// The calibration of the text block inside a TwoNav RMAP: the same keys as an .imp, without "Bitmap=" (the image
    /// is the RMAP itself). "Version=", when present, must be 2.
    /// </summary>
    /// <exception cref="LocalMapException">Not a valid calibration.</exception>
    public static MapCalibration ParseEmbedded(IReadOnlyList<string> lines, string rmapPath)
    {
        var values = KeyValues(lines);
        var version = values.FirstOrDefault(v => v.Key.Equals("Version", StringComparison.OrdinalIgnoreCase));
        if (version.Value is { } text && text != "2")
        {
            throw new LocalMapException(LocalMapError.InvalidCalibration, $"versión «{text}» de la calibración", version.Line);
        }

        return Parse(values, rmapPath, Path.GetFileNameWithoutExtension(rmapPath));
    }

    private static List<(string Key, string Value, int Line)> KeyValues(IReadOnlyList<string> lines) =>
        lines.Select((text, index) => (Text: text.Trim(), Line: index + 1))
            .Where(l => l.Text.Contains('=', StringComparison.Ordinal))
            .Select(l => (Key: l.Text[..l.Text.IndexOf('=', StringComparison.Ordinal)].Trim(), Value: l.Text[(l.Text.IndexOf('=', StringComparison.Ordinal) + 1)..].Trim(), l.Line))
            .ToList();

    private static MapCalibration Parse(List<(string Key, string Value, int Line)> values, string image, string title)
    {
        string? Value(string key) => values.FirstOrDefault(v => v.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;
        int LineOf(string key) => values.FirstOrDefault(v => v.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Line;

        var datum = OziMapReader.Datum(Value("Datum") ?? "WGS 84", LineOf("Datum"));
        var projection = Value("Projection") ?? string.Empty;
        if (projection.Length > 0 && !IsSupportedProjection(projection))
        {
            throw new LocalMapException(LocalMapError.UnsupportedCrs, $"proyección «{projection.TrimEnd(',')}»", LineOf("Projection"));
        }

        var raw = values
            .Where(v => v.Key.Length > 1 && v.Key[0] is 'P' or 'p' && v.Key[1..].All(char.IsDigit))
            .Select(v => (Fields: v.Value.Split(',').Select(f => f.Trim()).ToArray(), v.Line))
            .ToList();
        if (raw.Count == 0)
        {
            throw new LocalMapException(LocalMapError.InvalidCalibration, "sin puntos de calibración «Pn=»");
        }

        var geographic = raw.All(p => p.Fields.Length > 2 && p.Fields[2].Equals("A", StringComparison.OrdinalIgnoreCase));
        Crs crs;
        if (geographic)
        {
            crs = Crs.Geographic(datum);
        }
        else
        {
            var zoneText = raw.Select(p => p.Fields.Length > 2 ? p.Fields[2] : string.Empty).FirstOrDefault(z => !z.Equals("A", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
            crs = Crs.Utm(ParseZone(zoneText, out var north, raw[0].Line), north, datum);
        }

        var points = new List<ControlPoint>();
        foreach (var (fields, line) in raw)
        {
            if (fields.Length < 5
                || !OziMapReader.TryNumber(fields[0], out var px) || !OziMapReader.TryNumber(fields[1], out var py)
                || !OziMapReader.TryNumber(fields[3], out var x) || !OziMapReader.TryNumber(fields[4], out var y))
            {
                throw new LocalMapException(LocalMapError.InvalidCalibration, "punto de calibración incompleto", line);
            }

            if (fields[2].Equals("A", StringComparison.OrdinalIgnoreCase))
            {
                if (Math.Abs(y) > 90 || Math.Abs(x) > 180)
                {
                    throw new LocalMapException(LocalMapError.InvalidCalibration, "latitud o longitud fuera de rango", line);
                }

                var (mx, my) = crs.FromGeographic(y, x);
                points.Add(new ControlPoint(px, py, mx, my));
            }
            else
            {
                var zone = ParseZone(fields[2], out var north, line);
                if (zone != crs.UtmZone)
                {
                    var (lat, lon) = Crs.Utm(zone, north, datum).ToGeographic(x, y);
                    (x, y) = crs.FromGeographic(lat, lon);
                }

                points.Add(new ControlPoint(px, py, x, y));
            }
        }

        int? width = int.TryParse(Value("BitmapWidth"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var w) ? w : null;
        int? height = int.TryParse(Value("BitmapHeight"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var h) ? h : null;
        return new MapCalibration("CompeGPS", title, image, crs, points, width, height);
    }

    /// <summary>
    /// UTM ("0,UTM,30,N," in an RMAP, "2,UTM,30,N," in an .imp) or longitude/latitude, written in full
    /// ("1,Latitude/Longitude,") or short ("1,Lat/Long," in the RMAP of the IGN).
    /// </summary>
    private static bool IsSupportedProjection(string projection)
    {
        var name = projection.Split(',').Select(f => f.Trim()).FirstOrDefault(f => f.Length > 0 && !f.All(char.IsDigit)) ?? string.Empty;
        return name.Equals("UTM", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Latitude/Longitude", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Lat/Long", StringComparison.OrdinalIgnoreCase)
            || projection.Trim().TrimEnd(',') == "1";
    }

    /// <summary>"30T" → 30, north; latitude bands C–M are south.</summary>
    private static int ParseZone(string text, out bool north, int line)
    {
        var digits = new string(text.TakeWhile(char.IsDigit).ToArray());
        if (!int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var zone) || zone is < 1 or > 60)
        {
            throw new LocalMapException(LocalMapError.InvalidCalibration, $"huso UTM «{text}»", line);
        }

        var band = text.Length > digits.Length ? char.ToUpperInvariant(text[digits.Length]) : 'N';
        north = band >= 'N';
        return zone;
    }
}
