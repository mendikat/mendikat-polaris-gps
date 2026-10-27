using System.Globalization;
using System.Text.RegularExpressions;

namespace Mendikat.TrailLab.Mapping.Local;

/// <summary>
/// Reads the coordinate system of a .prj file (ESRI or OGC WKT, or a bare "EPSG:n") for ASCII grids (LDEM-04).
/// Supported: geographic and Transverse Mercator/UTM in metres, on WGS84, ETRS89 or ED50 (LMAP-04). Anything else is
/// rejected rather than guessed.
/// </summary>
public static partial class PrjReader
{
    /// <exception cref="UnsupportedCrsException">A system outside the supported list.</exception>
    public static Crs Parse(string text)
    {
        var wkt = text.Trim();
        if (Epsg().Match(wkt) is { Success: true } code && !wkt.Contains('[', StringComparison.Ordinal))
        {
            return FromEpsg(int.Parse(code.Groups[1].Value, CultureInfo.InvariantCulture));
        }

        if (!wkt.Contains('[', StringComparison.Ordinal) && ArcInfoKey().IsMatch(wkt))
        {
            return ParseArcInfo(wkt);
        }

        var datum = DatumOf(wkt);
        var upper = wkt.ToUpperInvariant();
        if (!upper.StartsWith("PROJCS", StringComparison.Ordinal) && !upper.StartsWith("PROJCRS", StringComparison.Ordinal))
        {
            if (upper.StartsWith("GEOGCS", StringComparison.Ordinal) || upper.StartsWith("GEOGCRS", StringComparison.Ordinal) || upper.StartsWith("GEODCRS", StringComparison.Ordinal))
            {
                return Crs.Geographic(datum);
            }

            throw new UnsupportedCrsException(Shorten(wkt));
        }

        var method = Projection().Match(wkt) is { Success: true } p ? p.Groups[2].Value : string.Empty;
        if (!method.Replace('_', ' ').Contains("Transverse Mercator", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnsupportedCrsException($"proyección «{method}»");
        }

        var unit = LinearUnit().Matches(wkt).Select(m => m.Groups[1].Value).LastOrDefault() ?? "metre";
        if (!unit.StartsWith("met", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnsupportedCrsException($"unidad «{unit}»");
        }

        var parameters = Parameter().Matches(wkt).ToDictionary(
            m => m.Groups[1].Value.Replace('_', ' ').ToLowerInvariant(),
            m => double.Parse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture));
        double Get(params string[] names) => names.Select(n => parameters.TryGetValue(n, out var v) ? (double?)v : null).FirstOrDefault(v => v is not null) ?? 0;
        var lat0 = Get("latitude of origin", "latitude of natural origin");
        var lon0 = Get("central meridian", "longitude of natural origin");
        var k = Get("scale factor", "scale factor at natural origin");
        var fe = Get("false easting");
        var fn = Get("false northing");
        if (k <= 0)
        {
            throw new UnsupportedCrsException("Transverse Mercator sin factor de escala");
        }

        // UTM: recognised so that ED50 uses the Helmert parameters of its zone, as mendikat.net.
        var zone = (lon0 + 183) / 6;
        if (lat0 == 0 && Math.Abs(k - 0.9996) < 1e-9 && fe == 500_000 && fn is 0 or 10_000_000 && Math.Abs(zone - Math.Round(zone)) < 1e-9)
        {
            return Crs.Utm((int)Math.Round(zone), fn == 0, datum);
        }

        return Crs.TransverseMercator(datum, lat0, lon0, k, fe, fn);
    }

    /// <summary>
    /// The classic ArcInfo .prj ("Projection UTM / Zone 30 / Datum WGS 84 / Units METERS"), as distributed with the
    /// IGN ASCII grids. A negative zone is in the southern hemisphere.
    /// </summary>
    private static Crs ParseArcInfo(string text)
    {
        var keys = text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2)
            .GroupBy(parts => parts[0], StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First()[1], StringComparer.OrdinalIgnoreCase);
        var projection = keys.GetValueOrDefault("Projection") ?? string.Empty;
        var datum = DatumOf(keys.GetValueOrDefault("Datum") ?? string.Empty);
        var units = keys.GetValueOrDefault("Units") ?? string.Empty;
        if (projection.Equals("GEOGRAPHIC", StringComparison.OrdinalIgnoreCase))
        {
            return Crs.Geographic(datum);
        }

        if (!projection.Equals("UTM", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnsupportedCrsException($"proyección «{projection}»");
        }

        if (!units.StartsWith("MET", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnsupportedCrsException($"unidad «{units}»");
        }

        if (!int.TryParse(keys.GetValueOrDefault("Zone"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var zone) || zone == 0 || Math.Abs(zone) > 60)
        {
            throw new UnsupportedCrsException($"huso «{keys.GetValueOrDefault("Zone")}»");
        }

        return Crs.Utm(Math.Abs(zone), zone > 0, datum);
    }

    internal static MapDatum DatumOf(string wkt)
    {
        var name = Datum().Match(wkt) is { Success: true } d ? d.Groups[1].Value : wkt;
        var normalized = name.Replace('_', ' ').ToUpperInvariant();
        if (normalized.Contains("REGCAN", StringComparison.Ordinal))
        {
            return MapDatum.Regcan95;
        }

        if (normalized.Contains("ETRS", StringComparison.Ordinal) || normalized.Contains("EUROPEAN TERRESTRIAL REFERENCE SYSTEM 1989", StringComparison.Ordinal))
        {
            return MapDatum.Etrs89;
        }

        if (normalized.Contains("WGS 1984", StringComparison.Ordinal) || normalized.Contains("WGS 84", StringComparison.Ordinal) || normalized.Contains("WGS84", StringComparison.Ordinal) || normalized.Contains("WORLD GEODETIC SYSTEM 1984", StringComparison.Ordinal))
        {
            return MapDatum.Wgs84;
        }

        if (normalized.Contains("EUROPEAN 1950", StringComparison.Ordinal) || normalized.Contains("ED50", StringComparison.Ordinal) || normalized.Contains("EUROPEAN DATUM 1950", StringComparison.Ordinal))
        {
            return MapDatum.Ed50;
        }

        throw new UnsupportedCrsException($"datum «{Shorten(name)}»");
    }

    private static Crs FromEpsg(int code) => code switch
    {
        4326 => Crs.Geographic(MapDatum.Wgs84),
        4258 => Crs.Geographic(MapDatum.Etrs89),
        4230 => Crs.Geographic(MapDatum.Ed50),
        >= 32601 and <= 32660 => Crs.Utm(code - 32600, true, MapDatum.Wgs84),
        >= 32701 and <= 32760 => Crs.Utm(code - 32700, false, MapDatum.Wgs84),
        >= 25828 and <= 25838 => Crs.Utm(code - 25800, true, MapDatum.Etrs89),
        >= 23028 and <= 23038 => Crs.Utm(code - 23000, true, MapDatum.Ed50),
        _ => throw new UnsupportedCrsException($"EPSG:{code}"),
    };

    private static string Shorten(string text) => text.Length <= 60 ? text : text[..60] + "…";

    [GeneratedRegex(@"^\s*Projection\s+\S", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex ArcInfoKey();

    [GeneratedRegex(@"^\s*EPSG:\s*(\d+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Epsg();

    [GeneratedRegex(@"DATUM\s*\[\s*""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex Datum();

    [GeneratedRegex(@"(PROJECTION|METHOD)\s*\[\s*""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex Projection();

    [GeneratedRegex(@"PARAMETER\s*\[\s*""([^""]+)""\s*,\s*([-+0-9.eE]+)", RegexOptions.IgnoreCase)]
    private static partial Regex Parameter();

    [GeneratedRegex(@"(?:LENGTHUNIT|UNIT)\s*\[\s*""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex LinearUnit();
}
