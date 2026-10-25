using System.Globalization;
using GeographicLib;
using Mendikat.TrailLab.Domain.Geo;

namespace Mendikat.TrailLab.Mapping.Coordinates;

/// <summary>
/// Status bar coordinate texts (COORD-01..05, ADR-0003). All values are WGS84; UTM/UPS are computed with
/// GeographicLib (Karney), which applies the Norway/Svalbard zone exceptions and switches to UPS near the poles.
/// </summary>
public static class CoordinateFormatter
{
    /// <summary>UTM latitude bands from 80° S, 8° each; X spans 72°–84° N. I and O are not used.</summary>
    private const string Bands = "CDEFGHJKLMNPQRSTUVWX";

    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>
    /// "30T 555555 4750000": zone + latitude band, easting then northing in whole meters (truncated, as in MGRS).
    /// Outside UTM (north of 84° N or south of 80° S) returns "UPS Z 2000000 1444542" (polar stereographic).
    /// </summary>
    public static string FormatUtm(GeoPosition position)
    {
        var (zone, north, easting, northing) = UTMUPS.Forward(position.Latitude, position.Longitude, (int)ZoneSpec.Standard, false);
        var e = Math.Floor(easting).ToString("0", CultureInfo.InvariantCulture);
        var n = Math.Floor(northing).ToString("0", CultureInfo.InvariantCulture);
        if (zone == 0)
        {
            return $"UPS {UpsLetter(north, position.Longitude)} {e} {n}";
        }

        return $"{zone}{Band(position.Latitude)} {e} {n}";
    }

    /// <summary>Latitude band letter for -80 ≤ latitude ≤ 84 (the band is not the zone).</summary>
    public static char Band(double latitude)
    {
        if (latitude is < -80 or > 84)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "UTM bands cover 80° S to 84° N.");
        }

        var index = (int)Math.Floor((latitude + 80) / 8);
        return Bands[Math.Min(index, Bands.Length - 1)];
    }

    /// <summary>"43º 22' 23,11'' N 1º 45' 10,00'' W": latitude first, seconds with two decimals and decimal comma.</summary>
    public static string FormatDms(GeoPosition position) =>
        $"{FormatDmsComponent(position.Latitude, 'N', 'S')} {FormatDmsComponent(position.Longitude, 'E', 'W')}";

    public static string FormatDmsComponent(double degrees, char positive, char negative)
    {
        // Integer hundredths of a second, so rounding carries correctly into minutes and degrees (59,995'' → next minute).
        var hundredths = (long)Math.Round(Math.Abs(degrees) * 360_000, MidpointRounding.AwayFromZero);
        var d = hundredths / 360_000;
        var m = hundredths % 360_000 / 6_000;
        var s = hundredths % 6_000 / 100m;
        var hemisphere = degrees < 0 && hundredths > 0 ? negative : positive;
        return string.Format(Spanish, "{0}º {1:00}' {2:00.00}'' {3}", d, m, s, hemisphere);
    }

    private static char UpsLetter(bool north, double longitude) =>
        north ? (longitude < 0 ? 'Y' : 'Z') : (longitude < 0 ? 'A' : 'B');
}
