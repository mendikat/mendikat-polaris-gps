using Mapsui.Projections;
using Mendikat.TrailLab.Domain.Geo;

namespace Mendikat.TrailLab.Mapping.Coordinates;

/// <summary>
/// The only conversion between WGS84 and the map's Web Mercator (EPSG:3857) coordinates (MAP-08, ADR-0003).
/// Web Mercator is used for drawing only; never for measuring.
/// </summary>
public static class WebMercator
{
    /// <summary>Latitude limit of the square Web Mercator world.</summary>
    public const double MaxLatitude = 85.05112878;

    public static (double X, double Y) ToWorld(GeoPosition position)
    {
        var lat = Math.Clamp(position.Latitude, -MaxLatitude, MaxLatitude);
        return SphericalMercator.FromLonLat(position.Longitude, lat);
    }

    /// <summary>Converts a world point back to WGS84, wrapping longitude into [-180, 180].</summary>
    public static GeoPosition ToGeo(double x, double y)
    {
        var (lon, lat) = SphericalMercator.ToLonLat(x, y);
        lon = ((lon + 180) % 360 + 360) % 360 - 180;
        return new GeoPosition(Math.Clamp(lat, -90, 90), lon);
    }
}
