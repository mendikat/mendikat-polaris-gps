using Mendikat.TrailLab.Domain.Geo;

namespace Mendikat.TrailLab.Mapping.Local;

/// <summary>
/// Datum conversions to and from WGS84 (LMAP-04). ED50 uses the same 7-parameter Helmert transformation as
/// mendikat.net (<c>lib/GIS/HelmertTransformation.php</c>, <c>Geoposition::applyHelmertTransformation</c>): one
/// parameter set per UTM zone 29, 30 and 31, rotations in arc seconds, scale in ppm, applied to geocentric cartesian
/// coordinates; the inverse uses the same parameters with every sign reversed, as the PHP does. ETRS89 is taken as
/// WGS84 (they differ by less than 1 m in Europe).
/// </summary>
public static class DatumShift
{
    private sealed record Helmert(double Dx, double Dy, double Dz, double Rx, double Ry, double Rz, double S)
    {
        public Helmert Reversed() => new(-Dx, -Dy, -Dz, -Rx, -Ry, -Rz, -S);
    }

    /// <exception cref="UnsupportedCrsException">ED50 outside zones 29–31.</exception>
    public static GeoPosition ToWgs84(MapDatum datum, double latitude, double longitude, int? utmZone = null)
    {
        if (datum != MapDatum.Ed50)
        {
            return new GeoPosition(latitude, longitude);
        }

        var (lat, lon) = Apply(Ed50ToWgs84(utmZone ?? ZoneOf(longitude)), latitude, longitude, MapDatum.Ed50, MapDatum.Wgs84);
        return new GeoPosition(lat, lon);
    }

    /// <exception cref="UnsupportedCrsException">ED50 outside zones 29–31.</exception>
    public static (double Latitude, double Longitude) FromWgs84(MapDatum datum, double latitude, double longitude, int? utmZone = null) =>
        datum != MapDatum.Ed50
            ? (latitude, longitude)
            : Apply(Ed50ToWgs84(utmZone ?? ZoneOf(longitude)).Reversed(), latitude, longitude, MapDatum.Wgs84, MapDatum.Ed50);

    /// <summary>UTM zone of a longitude (no Norway/Svalbard exceptions: ED50 is only supported in Spain).</summary>
    public static int ZoneOf(double longitude) => Math.Clamp((int)Math.Floor((longitude + 180) / 6) + 1, 1, 60);

    /// <summary>The IGN parameters used by mendikat.net (<c>HelmertTransformation::getED50toWGS84</c>).</summary>
    private static Helmert Ed50ToWgs84(int zone) => zone switch
    {
        29 => new(-178.383, -83.172, -221.293, 0.5401, -0.5319, -0.1263, 21.2),
        30 => new(-131.032, -100.251, -163.354, -1.2438, -0.0195, -1.1436, 9.3921),
        31 => new(-181.4609, -90.2931, -187.1902, 0.1435, 0.4922, -0.3935, 17.57),
        _ => throw new UnsupportedCrsException($"ED50 en el huso {zone} (sólo 29, 30 y 31)"),
    };

    private static (double Latitude, double Longitude) Apply(Helmert h, double latitude, double longitude, MapDatum from, MapDatum to)
    {
        var (x, y, z) = ToGeocentric(latitude, longitude, Crs.Ellipsoid(from));
        const double SecondsToRadians = Math.PI / (180 * 3600);
        var rx = h.Rx * SecondsToRadians;
        var ry = h.Ry * SecondsToRadians;
        var rz = h.Rz * SecondsToRadians;
        var s = h.S * 1e-6;
        var xp = h.Dx + ((1 + s) * (x - (rz * y) + (ry * z)));
        var yp = h.Dy + ((1 + s) * ((rz * x) + y - (rx * z)));
        var zp = h.Dz + ((1 + s) * ((-ry * x) + (rx * y) + z));
        return FromGeocentric(xp, yp, zp, Crs.Ellipsoid(to));
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

    /// <summary>Geocentric cartesian to geodetic latitude and longitude (iterated to below 1e-12 rad).</summary>
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
