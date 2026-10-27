using GeographicLib;
using GeographicLib.Projections;
using Mendikat.TrailLab.Domain.Geo;

namespace Mendikat.TrailLab.Mapping.Local;

/// <summary>Horizontal datums of local maps and DEMs (LMAP-04). Positions are converted to WGS84 for display.</summary>
public enum MapDatum
{
    Wgs84,

    /// <summary>Equal to WGS84 at map precision (below 1 m in Europe); no transformation is applied.</summary>
    Etrs89,

    /// <summary>Canary Islands (IGN); like ETRS89, equal to WGS84 at map precision; no transformation is applied.</summary>
    Regcan95,

    /// <summary>European 1950 on the International 1924 ellipsoid; 7-parameter Helmert to WGS84 as mendikat.net.</summary>
    Ed50,
}

/// <summary>
/// A coordinate reference system: a datum and a projection (geographic, or Transverse Mercator such as UTM).
/// Geographic coordinates are (x = longitude, y = latitude) in degrees; projected ones are meters.
/// </summary>
public sealed class Crs
{
    private readonly TransverseMercator? _tm;
    private readonly double _originNorthing;

    private Crs(MapDatum datum, string description, double centralMeridian = 0, double scale = 0, double falseEasting = 0, double falseNorthing = 0, double latitudeOfOrigin = 0, int? utmZone = null)
    {
        Datum = datum;
        Description = description;
        CentralMeridian = centralMeridian;
        FalseEasting = falseEasting;
        FalseNorthing = falseNorthing;
        UtmZone = utmZone;
        if (scale > 0)
        {
            var (a, f) = Ellipsoid(datum);
            _tm = new TransverseMercator(a, f, scale);
            (_, _originNorthing) = _tm.Forward(centralMeridian, latitudeOfOrigin, centralMeridian);
        }
    }

    public MapDatum Datum { get; }

    /// <summary>Short text for the user, e.g. "UTM 30N ED50".</summary>
    public string Description { get; }

    public bool IsGeographic => _tm is null;

    public double CentralMeridian { get; }

    public double FalseEasting { get; }

    public double FalseNorthing { get; }

    /// <summary>The UTM zone of a UTM system, or null.</summary>
    public int? UtmZone { get; }

    public static Crs Geographic(MapDatum datum) => new(datum, $"{Name(datum)} geográficas");

    /// <exception cref="ArgumentOutOfRangeException">Zone outside 1–60.</exception>
    public static Crs Utm(int zone, bool north, MapDatum datum)
    {
        if (zone is < 1 or > 60)
        {
            throw new ArgumentOutOfRangeException(nameof(zone), zone, "UTM zones are 1 to 60.");
        }

        return new Crs(datum, $"UTM {zone}{(north ? "N" : "S")} {Name(datum)}", -183 + (6 * zone), 0.9996, 500_000, north ? 0 : 10_000_000, 0, zone);
    }

    public static Crs TransverseMercator(MapDatum datum, double latitudeOfOrigin, double centralMeridian, double scale, double falseEasting, double falseNorthing) =>
        new(datum, $"Transverse Mercator {centralMeridian:0.###}° {Name(datum)}", centralMeridian, scale, falseEasting, falseNorthing, latitudeOfOrigin);

    public static string Name(MapDatum datum) => datum switch
    {
        MapDatum.Etrs89 => "ETRS89",
        MapDatum.Regcan95 => "REGCAN95",
        MapDatum.Ed50 => "ED50",
        _ => "WGS84",
    };

    /// <summary>Converts coordinates of this system to a WGS84 position.</summary>
    /// <exception cref="UnsupportedCrsException">ED50 outside the UTM zones 29–31 covered by the transformation.</exception>
    public GeoPosition ToWgs84(double x, double y)
    {
        var (lat, lon) = ToGeographic(x, y);
        return DatumShift.ToWgs84(Datum, lat, lon, UtmZone);
    }

    /// <summary>Converts a WGS84 position to coordinates of this system.</summary>
    public (double X, double Y) FromWgs84(GeoPosition position)
    {
        var (lat, lon) = DatumShift.FromWgs84(Datum, position.Latitude, position.Longitude, UtmZone);
        return FromGeographic(lat, lon);
    }

    /// <summary>Latitude and longitude on this system's own datum.</summary>
    public (double Latitude, double Longitude) ToGeographic(double x, double y)
    {
        if (_tm is null)
        {
            return (y, x);
        }

        var (lat, lon) = _tm.Reverse(CentralMeridian, x - FalseEasting, y - FalseNorthing + _originNorthing);
        return (lat, lon);
    }

    public (double X, double Y) FromGeographic(double latitude, double longitude)
    {
        if (_tm is null)
        {
            return (longitude, latitude);
        }

        var (x, y) = _tm.Forward(CentralMeridian, latitude, longitude);
        return (x + FalseEasting, y - _originNorthing + FalseNorthing);
    }

    internal static (double A, double F) Ellipsoid(MapDatum datum) => datum == MapDatum.Ed50
        ? (6_378_388, 1 / 297.0) // International 1924 (Hayford)
        : (6_378_137, 1 / 298.257223563); // WGS84

    public override string ToString() => Description;
}

/// <summary>A coordinate system or datum that local maps do not support (LMAP-04).</summary>
public sealed class UnsupportedCrsException(string detail) : Exception($"Unsupported coordinate system: {detail}")
{
    public string Detail { get; } = detail;
}
