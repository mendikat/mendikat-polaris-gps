using System.Globalization;
using System.Text;

namespace Polaris.Gps.Mapping.Local;

/// <summary>
/// Reads OziExplorer calibration files (.map, "OziExplorer Map Data File Version 2.x"; LMAP-03, LMAP-04). Line 3 is
/// the image, line 5 the datum, "Map Projection" and "Projection Setup" the projection, and "Point01".."Point30" the
/// calibration points, given as latitude/longitude (on the map datum) or as grid coordinates. Without at least two
/// usable points, the corners in "MMPXY"/"MMPLL" (WGS84) are used. Files are Windows-1252 text.
/// </summary>
public static class OziMapReader
{
    private const string Signature = "OziExplorer Map Data File";

    /// <exception cref="LocalMapException">Not a valid calibration, or its image is missing.</exception>
    public static MapCalibration Read(string path)
    {
        var lines = ReadLines(path);
        if (lines.Length < 5 || !lines[0].StartsWith(Signature, StringComparison.OrdinalIgnoreCase))
        {
            throw new LocalMapException(LocalMapError.InvalidCalibration, "no es un archivo .map de OziExplorer", 1);
        }

        var image = LocalFiles.ResolveImage(path, lines[2].Trim());
        var datum = Datum(lines[4].Split(',')[0].Trim(), 5);
        var projectionLine = Find(lines, "Map Projection");
        var projection = projectionLine is { } p ? Field(p.Fields, 1) : "Latitude/Longitude";
        var points = lines.Select((text, index) => (Fields: Split(text), Line: index + 1))
            .Where(l => l.Fields.Length > 0 && l.Fields[0].StartsWith("Point", StringComparison.OrdinalIgnoreCase) && l.Fields[0].Length > 5)
            .Select(l => RawPoint.Parse(l.Fields, l.Line))
            .OfType<RawPoint>()
            .ToList();

        var crs = projection.ToUpperInvariant() switch
        {
            "LATITUDE/LONGITUDE" => Crs.Geographic(datum),
            "UTM GRID" or "(UTM) UNIVERSAL TRANSVERSE MERCATOR" => UtmFor(points, datum),
            "TRANSVERSE MERCATOR" => TransverseMercator(Find(lines, "Projection Setup"), datum),
            _ => throw new LocalMapException(LocalMapError.UnsupportedCrs, $"proyección «{projection}»", projectionLine?.Line),
        };

        var control = points.Select(raw => raw.ToControlPoint(crs)).OfType<ControlPoint>().ToList();
        if (control.Count < 2)
        {
            control = Corners(lines, crs);
        }

        if (control.Count < 2)
        {
            throw new LocalMapException(LocalMapError.InvalidCalibration, $"{control.Count} puntos de calibración (mínimo 2)");
        }

        var size = Find(lines, "IWH");
        int? width = size is { } s && int.TryParse(Field(s.Fields, 2), out var w) ? w : null;
        int? height = size is { } t && int.TryParse(Field(t.Fields, 3), out var h) ? h : null;
        return new MapCalibration("OziExplorer", lines[1].Trim(), image, crs, control, width, height);
    }

    /// <summary>OziExplorer datum names, as mendikat.net reads them (<c>Oziexplorer/Parsers/DatumParser.php</c>), plus ETRS89.</summary>
    internal static MapDatum Datum(string name, int line)
    {
        if (name.Contains("WGS 84", StringComparison.OrdinalIgnoreCase) || name.Contains("WGS84", StringComparison.OrdinalIgnoreCase))
        {
            return MapDatum.Wgs84;
        }

        if (name.Contains("ETRS", StringComparison.OrdinalIgnoreCase))
        {
            return MapDatum.Etrs89;
        }

        if (name.Contains("European 1950", StringComparison.OrdinalIgnoreCase) || name.Contains("ED50", StringComparison.OrdinalIgnoreCase))
        {
            return MapDatum.Ed50;
        }

        throw new LocalMapException(LocalMapError.UnsupportedCrs, $"datum «{name}»", line);
    }

    private static string[] ReadLines(string path)
    {
        try
        {
            return File.ReadAllLines(path, Encoding.Latin1);
        }
        catch (IOException ex)
        {
            throw new LocalMapException(LocalMapError.ReadFailed, Path.GetFileName(path), innerException: ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new LocalMapException(LocalMapError.ReadFailed, Path.GetFileName(path), innerException: ex);
        }
    }

    private static Crs UtmFor(List<RawPoint> points, MapDatum datum)
    {
        if (points.FirstOrDefault(p => p.Zone is not null) is { Zone: { } zone } grid)
        {
            return Crs.Utm(zone, grid.North, datum);
        }

        if (points.FirstOrDefault(p => p.Latitude is not null) is { Latitude: { } lat, Longitude: { } lon })
        {
            return Crs.Utm(DatumShift.ZoneOf(lon), lat >= 0, datum);
        }

        throw new LocalMapException(LocalMapError.InvalidCalibration, "UTM sin huso en los puntos de calibración");
    }

    /// <summary>"Projection Setup, lat origin, lon origin, K factor, false easting, false northing, …".</summary>
    private static Crs TransverseMercator((string[] Fields, int Line)? setup, MapDatum datum)
    {
        if (setup is not { } s
            || !TryNumber(Field(s.Fields, 1), out var lat0) || !TryNumber(Field(s.Fields, 2), out var lon0)
            || !TryNumber(Field(s.Fields, 3), out var k) || !TryNumber(Field(s.Fields, 4), out var fe)
            || !TryNumber(Field(s.Fields, 5), out var fn) || k <= 0)
        {
            throw new LocalMapException(LocalMapError.InvalidCalibration, "Transverse Mercator sin «Projection Setup» completo", setup?.Line);
        }

        return Crs.TransverseMercator(datum, lat0, lon0, k, fe, fn);
    }

    /// <summary>MMPXY (pixels) and MMPLL (WGS84 longitude, latitude) of the map corners.</summary>
    private static List<ControlPoint> Corners(string[] lines, Crs crs)
    {
        var pixels = lines.Select(Split).Where(f => Field(f, 0).Equals("MMPXY", StringComparison.OrdinalIgnoreCase)).ToList();
        var geo = lines.Select(Split).Where(f => Field(f, 0).Equals("MMPLL", StringComparison.OrdinalIgnoreCase)).ToList();
        var points = new List<ControlPoint>();
        foreach (var pixel in pixels)
        {
            var match = geo.FirstOrDefault(g => Field(g, 1) == Field(pixel, 1));
            if (match is not null
                && TryNumber(Field(pixel, 2), out var px) && TryNumber(Field(pixel, 3), out var py)
                && TryNumber(Field(match, 2), out var lon) && TryNumber(Field(match, 3), out var lat))
            {
                var (mapLat, mapLon) = DatumShift.FromWgs84(crs.Datum, lat, lon, crs.UtmZone);
                var (x, y) = crs.FromGeographic(mapLat, mapLon);
                points.Add(new ControlPoint(px, py, x, y));
            }
        }

        return points;
    }

    private static (string[] Fields, int Line)? Find(string[] lines, string key)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith(key, StringComparison.OrdinalIgnoreCase))
            {
                return (Split(lines[i]), i + 1);
            }
        }

        return null;
    }

    private static string[] Split(string line) => line.Split(',').Select(f => f.Trim()).ToArray();

    private static string Field(string[] fields, int index) => index < fields.Length ? fields[index] : string.Empty;

    internal static bool TryNumber(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    /// <summary>One "PointNN" line; null when the point is not used (no pixel coordinates).</summary>
    private sealed record RawPoint(double PixelX, double PixelY, double? Latitude, double? Longitude, int? Zone, double? Easting, double? Northing, bool North, int Line)
    {
        // Point01,xy, px, py,in, deg, latdeg, latmin, N, londeg, lonmin, W, grid, zone, easting, northing, N
        public static RawPoint? Parse(string[] f, int line)
        {
            if (!TryNumber(Field(f, 2), out var px) || !TryNumber(Field(f, 3), out var py))
            {
                return null;
            }

            double? lat = null, lon = null;
            if (TryNumber(Field(f, 6), out var latDeg) && TryNumber(Field(f, 9), out var lonDeg))
            {
                TryNumber(Field(f, 7), out var latMin);
                TryNumber(Field(f, 10), out var lonMin);
                lat = (latDeg + (latMin / 60)) * (Field(f, 8).Equals("S", StringComparison.OrdinalIgnoreCase) ? -1 : 1);
                lon = (lonDeg + (lonMin / 60)) * (Field(f, 11).Equals("W", StringComparison.OrdinalIgnoreCase) ? -1 : 1);
                if (Math.Abs(lat.Value) > 90 || Math.Abs(lon.Value) > 180)
                {
                    throw new LocalMapException(LocalMapError.InvalidCalibration, $"latitud o longitud fuera de rango en {Field(f, 0)}", line);
                }
            }

            int? zone = int.TryParse(Field(f, 13), NumberStyles.Integer, CultureInfo.InvariantCulture, out var z) && z is >= 1 and <= 60 ? z : null;
            double? easting = TryNumber(Field(f, 14), out var e) ? e : null;
            double? northing = TryNumber(Field(f, 15), out var n) ? n : null;
            var north = !Field(f, 16).Equals("S", StringComparison.OrdinalIgnoreCase);
            if (lat is null && (easting is null || northing is null))
            {
                return null; // pixel without coordinates: not a calibration point
            }

            return new RawPoint(px, py, lat, lon, zone, easting, northing, north, line);
        }

        public ControlPoint? ToControlPoint(Crs crs)
        {
            if (Latitude is { } lat && Longitude is { } lon)
            {
                var (x, y) = crs.FromGeographic(lat, lon);
                return new ControlPoint(PixelX, PixelY, x, y);
            }

            if (Easting is not { } e || Northing is not { } n)
            {
                return null;
            }

            if (crs.IsGeographic)
            {
                throw new LocalMapException(LocalMapError.InvalidCalibration, "punto en coordenadas de cuadrícula en un mapa Latitude/Longitude", Line);
            }

            if (Zone is { } zone && crs.UtmZone is { } mapZone && zone != mapZone)
            {
                // A point given in a neighbouring UTM zone: through geographic coordinates on the same datum.
                var (lat2, lon2) = Crs.Utm(zone, North, crs.Datum).ToGeographic(e, n);
                var (x, y) = crs.FromGeographic(lat2, lon2);
                return new ControlPoint(PixelX, PixelY, x, y);
            }

            return new ControlPoint(PixelX, PixelY, e, n);
        }
    }
}
