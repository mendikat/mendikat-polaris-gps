using GeographicLib;
using Polaris.Gps.Domain.Model;

namespace Polaris.Gps.Domain.Geo;

/// <summary>
/// Geodesic calculations on the WGS84 ellipsoid with Karney's algorithms (GeographicLib), ADR-0003/ADR-0008.
/// Distances are horizontal: elevation is ignored.
/// </summary>
public static class Geodesy
{
    /// <summary>Geodesic distance in meters.</summary>
    public static double Distance(GeoPosition a, GeoPosition b)
    {
        Geodesic.WGS84.Inverse(a.Latitude, a.Longitude, b.Latitude, b.Longitude, out double meters);
        return meters;
    }

    /// <summary>Point halfway along the geodesic from <paramref name="a"/> to <paramref name="b"/>.</summary>
    public static GeoPosition Midpoint(GeoPosition a, GeoPosition b)
    {
        Geodesic.WGS84.Inverse(a.Latitude, a.Longitude, b.Latitude, b.Longitude, out double meters, out double azimuth, out _);
        Geodesic.WGS84.Direct(a.Latitude, a.Longitude, azimuth, meters / 2, out double lat, out double lon);
        return new GeoPosition(lat, NormalizeLongitude(lon));
    }

    /// <summary>Length of a polyline in meters (sum of consecutive geodesic distances).</summary>
    public static double Length(IReadOnlyList<TrailPoint> points)
    {
        var total = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            total += Distance(points[i - 1].Position, points[i].Position);
        }

        return total;
    }

    /// <summary>Length of a track: segments are measured separately, the gaps between them are not counted (PROF-02).</summary>
    public static double Length(Track track) => track.Segments.Sum(s => Length(s.Points));

    private static double NormalizeLongitude(double lon) => ((lon + 180) % 360 + 360) % 360 - 180;
}
