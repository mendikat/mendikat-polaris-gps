using Polaris.Gps.Domain.Geo;

namespace Polaris.Gps.Mapping.Local;

/// <summary>
/// Datum conversions to and from WGS84 (LMAP-04). ED50 uses the Helmert transformation of mendikat.net
/// (<see cref="Ed50"/>); ETRS89 is taken as WGS84 (they differ by less than 1 m in Europe).
/// </summary>
public static class DatumShift
{
    /// <exception cref="UnsupportedCrsException">ED50 outside zones 29–31.</exception>
    public static GeoPosition ToWgs84(MapDatum datum, double latitude, double longitude, int? utmZone = null)
    {
        if (datum != MapDatum.Ed50)
        {
            return new GeoPosition(latitude, longitude);
        }

        var (lat, lon) = Ed50.ToWgs84(latitude, longitude, CheckedZone(utmZone ?? ZoneOf(longitude)));
        return new GeoPosition(lat, lon);
    }

    /// <exception cref="UnsupportedCrsException">ED50 outside zones 29–31.</exception>
    public static (double Latitude, double Longitude) FromWgs84(MapDatum datum, double latitude, double longitude, int? utmZone = null) =>
        datum != MapDatum.Ed50
            ? (latitude, longitude)
            : Ed50.FromWgs84(latitude, longitude, CheckedZone(utmZone ?? ZoneOf(longitude)));

    /// <summary>UTM zone of a longitude (no Norway/Svalbard exceptions: ED50 is only supported in Spain).</summary>
    public static int ZoneOf(double longitude) => Ed50.ZoneOf(longitude);

    private static int CheckedZone(int zone) => Ed50.IsSupportedZone(zone)
        ? zone
        : throw new UnsupportedCrsException($"ED50 en el huso {zone} (sólo 29, 30 y 31)");
}
