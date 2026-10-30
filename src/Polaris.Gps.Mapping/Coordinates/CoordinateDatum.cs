using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Mapping.Local;

namespace Polaris.Gps.Mapping.Coordinates;

/// <summary>
/// Datum in which coordinates are shown and typed (File › Settings › Coordinates). Positions are always kept and saved
/// in WGS84; only their texts change.
/// </summary>
public enum CoordinateDatum
{
    Wgs84,

    /// <summary>European 1950, with the Helmert transformation of mendikat.net; only in UTM zones 29–31 (Spain and Portugal).</summary>
    Ed50,
}

/// <summary>Conversions between WGS84 positions and the coordinates of a <see cref="CoordinateDatum"/>.</summary>
public static class CoordinateDatums
{
    public static IReadOnlyList<CoordinateDatum> All { get; } = [CoordinateDatum.Wgs84, CoordinateDatum.Ed50];

    public static string Name(CoordinateDatum datum) => datum == CoordinateDatum.Ed50 ? "ED50" : "WGS84";

    /// <summary>The datum named <paramref name="name"/> ("WGS84", "ED50"); WGS84 for anything else.</summary>
    public static CoordinateDatum FromName(string? name) =>
        string.Equals(name?.Trim(), "ED50", StringComparison.OrdinalIgnoreCase) ? CoordinateDatum.Ed50 : CoordinateDatum.Wgs84;

    /// <summary>
    /// The datum in which a WGS84 position is actually shown: ED50 only inside its UTM zones 29–31; elsewhere (Canary
    /// Islands, rest of the world) the coordinates stay in WGS84.
    /// </summary>
    public static CoordinateDatum Effective(CoordinateDatum datum, GeoPosition position) =>
        datum == CoordinateDatum.Ed50 && Ed50.IsSupportedZone(Ed50.ZoneOf(position.Longitude)) ? CoordinateDatum.Ed50 : CoordinateDatum.Wgs84;

    /// <summary>Latitude and longitude of a WGS84 position in <see cref="Effective"/> datum.</summary>
    public static (double Latitude, double Longitude) FromWgs84(GeoPosition position, CoordinateDatum datum) =>
        Effective(datum, position) == CoordinateDatum.Ed50
            ? Ed50.FromWgs84(position.Latitude, position.Longitude)
            : (position.Latitude, position.Longitude);

    /// <summary>
    /// The WGS84 position of coordinates typed in <paramref name="datum"/>. ED50 coordinates outside zones 29–31 are
    /// taken as WGS84, as they are shown.
    /// </summary>
    public static GeoPosition ToWgs84(double latitude, double longitude, CoordinateDatum datum)
    {
        if (datum == CoordinateDatum.Ed50 && Ed50.IsSupportedZone(Ed50.ZoneOf(longitude)))
        {
            var (lat, lon) = Ed50.ToWgs84(latitude, longitude);
            return new GeoPosition(lat, lon);
        }

        return new GeoPosition(latitude, longitude);
    }

    /// <summary>UTM easting and northing of a WGS84 position on the ED50 datum (International 1924 ellipsoid), in its own zone.</summary>
    public static (int Zone, double Easting, double Northing) Ed50Utm(GeoPosition position)
    {
        var zone = Ed50.ZoneOf(position.Longitude);
        var (x, y) = Crs.Utm(zone, position.Latitude >= 0, MapDatum.Ed50).FromWgs84(position);
        return (zone, x, y);
    }
}
