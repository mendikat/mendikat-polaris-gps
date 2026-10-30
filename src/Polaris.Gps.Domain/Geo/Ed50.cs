namespace Polaris.Gps.Domain.Geo;

/// <summary>
/// ED50 ↔ WGS84 with the same 7-parameter Helmert transformation as mendikat.net (<c>lib/GIS/HelmertTransformation.php</c>,
/// <c>Geoposition::applyHelmertTransformation</c>): one IGN parameter set per UTM zone 29, 30 and 31, rotations in arc
/// seconds, scale in ppm, applied to geocentric cartesian coordinates; the inverse uses the same parameters with every
/// sign reversed, as the PHP does. The elevation is never shifted.
/// </summary>
public static class Ed50
{
    private static readonly (double A, double F) International1924 = (6_378_388, 1 / 297.0);
    private static readonly (double A, double F) Wgs84 = (6_378_137, 1 / 298.257223563);

    private sealed record Helmert(double Dx, double Dy, double Dz, double Rx, double Ry, double Rz, double S)
    {
        public Helmert Reversed() => new(-Dx, -Dy, -Dz, -Rx, -Ry, -Rz, -S);
    }

    /// <summary>True for the UTM zones covered by the transformation (29, 30 and 31: Spain and Portugal).</summary>
    public static bool IsSupportedZone(int zone) => zone is >= 29 and <= 31;

    /// <summary>UTM zone of a longitude (no Norway/Svalbard exceptions: ED50 is only supported in Spain).</summary>
    public static int ZoneOf(double longitude) => Math.Clamp((int)Math.Floor((longitude + 180) / 6) + 1, 1, 60);

    /// <param name="utmZone">Zone whose parameters are used; by default, the zone of <paramref name="longitude"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Outside zones 29–31.</exception>
    public static (double Latitude, double Longitude) ToWgs84(double latitude, double longitude, int? utmZone = null) =>
        Apply(ToWgs84Parameters(utmZone ?? ZoneOf(longitude)), latitude, longitude, International1924, Wgs84);

    /// <exception cref="ArgumentOutOfRangeException">Outside zones 29–31.</exception>
    public static (double Latitude, double Longitude) FromWgs84(double latitude, double longitude, int? utmZone = null) =>
        Apply(ToWgs84Parameters(utmZone ?? ZoneOf(longitude)).Reversed(), latitude, longitude, Wgs84, International1924);

    /// <summary>The IGN parameters used by mendikat.net (<c>HelmertTransformation::getED50toWGS84</c>).</summary>
    private static Helmert ToWgs84Parameters(int zone) => zone switch
    {
        29 => new(-178.383, -83.172, -221.293, 0.5401, -0.5319, -0.1263, 21.2),
        30 => new(-131.032, -100.251, -163.354, -1.2438, -0.0195, -1.1436, 9.3921),
        31 => new(-181.4609, -90.2931, -187.1902, 0.1435, 0.4922, -0.3935, 17.57),
        _ => throw new ArgumentOutOfRangeException(nameof(zone), zone, "ED50 is only supported in UTM zones 29, 30 and 31."),
    };

    private static (double Latitude, double Longitude) Apply(
        Helmert h, double latitude, double longitude, (double A, double F) from, (double A, double F) to)
    {
        var (x, y, z) = ToGeocentric(latitude, longitude, from);
        const double SecondsToRadians = Math.PI / (180 * 3600);
        var rx = h.Rx * SecondsToRadians;
        var ry = h.Ry * SecondsToRadians;
        var rz = h.Rz * SecondsToRadians;
        var s = h.S * 1e-6;
        var xp = h.Dx + ((1 + s) * (x - (rz * y) + (ry * z)));
        var yp = h.Dy + ((1 + s) * ((rz * x) + y - (rx * z)));
        var zp = h.Dz + ((1 + s) * ((-ry * x) + (rx * y) + z));
        return FromGeocentric(xp, yp, zp, to);
    }

    /// <summary>Geodetic (height 0) to geocentric cartesian, as the elevation is never shifted.</summary>
    private static (double X, double Y, double Z) ToGeocentric(double latitude, double longitude, (double A, double F) ellipsoid)
    {
        var (a, f) = ellipsoid;
        var e2 = f * (2 - f);
        var phi = latitude * Math.PI / 180;
        var lambda = longitude * Math.PI / 180;
        var n = a / Math.Sqrt(1 - (e2 * Math.Sin(phi) * Math.Sin(phi)));
        return (n * Math.Cos(phi) * Math.Cos(lambda), n * Math.Cos(phi) * Math.Sin(lambda), n * (1 - e2) * Math.Sin(phi));
    }

    /// <summary>
    /// Geocentric cartesian to geodetic latitude and longitude, iterated to below 1e-12 rad. mendikat.net stops at
    /// 1e-6 rad and returns the previous iterate, about 0.3 m off in latitude (D12); Polaris GPS converges fully.
    /// </summary>
    private static (double Latitude, double Longitude) FromGeocentric(double x, double y, double z, (double A, double F) ellipsoid)
    {
        var (a, f) = ellipsoid;
        var e2 = f * (2 - f);
        var p = Math.Sqrt((x * x) + (y * y));
        var phi = Math.Atan2(z, p * (1 - e2));
        for (var i = 0; i < 10; i++)
        {
            var n = a / Math.Sqrt(1 - (e2 * Math.Sin(phi) * Math.Sin(phi)));
            var h = (p / Math.Cos(phi)) - n;
            var next = Math.Atan2(z, p * (1 - (e2 * n / (n + h))));
            if (Math.Abs(next - phi) < 1e-12)
            {
                phi = next;
                break;
            }

            phi = next;
        }

        return (phi * 180 / Math.PI, Math.Atan2(y, x) * 180 / Math.PI);
    }
}
