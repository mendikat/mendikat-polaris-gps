using System.Globalization;
using GeographicLib;
using Polaris.Gps.Domain.Geo;

namespace Polaris.Gps.Mapping.Coordinates;

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

    /// <summary>
    /// UTM text of a WGS84 position in <paramref name="datum"/>: as <see cref="FormatUtm(GeoPosition)"/> for WGS84, or on
    /// the ED50 datum inside its zones 29–31 (elsewhere WGS84, see <see cref="CoordinateDatums.Effective"/>).
    /// </summary>
    public static string FormatUtm(GeoPosition position, CoordinateDatum datum)
    {
        if (CoordinateDatums.Effective(datum, position) != CoordinateDatum.Ed50)
        {
            return FormatUtm(position);
        }

        var (zone, easting, northing) = CoordinateDatums.Ed50Utm(position);
        var (latitude, _) = CoordinateDatums.FromWgs84(position, datum);
        return string.Create(CultureInfo.InvariantCulture, $"{zone}{Band(latitude)} {Math.Floor(easting):0} {Math.Floor(northing):0}");
    }

    /// <summary>Degrees, minutes and seconds of a WGS84 position in <paramref name="datum"/> (see <see cref="CoordinateDatums.Effective"/>).</summary>
    public static string FormatDms(GeoPosition position, CoordinateDatum datum)
    {
        var (latitude, longitude) = CoordinateDatums.FromWgs84(position, datum);
        return FormatDms(new GeoPosition(latitude, longitude));
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
