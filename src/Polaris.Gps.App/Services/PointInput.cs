using System.Globalization;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Gpx;
using Polaris.Gps.Mapping.Coordinates;

namespace Polaris.Gps.App.Services;

/// <summary>Parsing and display of point values typed by the user (EDIT-04, WPE-03). Display uses Spanish format.</summary>
public static class PointInput
{
    public const double MinElevation = -500;
    public const double MaxElevation = 9000;

    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private static readonly string[] UtcFormats = ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ssZ"];

    public static string FormatCoordinate(double degrees) => degrees.ToString("0.0000000", Spanish);

    /// <summary>
    /// Latitude and longitude texts of a WGS84 position in <paramref name="datum"/>, the datum of the coordinates shown
    /// and typed (File › Settings › Coordinates; see <see cref="CoordinateDatums.Effective"/>).
    /// </summary>
    public static (string Latitude, string Longitude) FormatPosition(GeoPosition position, CoordinateDatum datum = CoordinateDatum.Wgs84)
    {
        var (latitude, longitude) = CoordinateDatums.FromWgs84(position, datum);
        return (FormatCoordinate(latitude), FormatCoordinate(longitude));
    }

    public static string FormatElevation(double? meters) => meters?.ToString("0.0", Spanish) ?? string.Empty;

    public static string FormatUtc(DateTimeOffset? time) =>
        time?.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty;

    public static string FormatLocal(DateTimeOffset? time) =>
        time is { } t ? TimeZoneInfo.ConvertTime(t, TimeZoneInfo.Local).ToString("dd/MM/yyyy HH:mm:ss", Spanish) : string.Empty;

    /// <summary>Accepts a decimal comma or point; thousands separators are not allowed (avoids "42.5" → 425).</summary>
    public static bool TryParseNumber(string? text, out double value)
    {
        value = 0;
        var normalized = text?.Trim().Replace(',', '.');
        return !string.IsNullOrEmpty(normalized)
            && double.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)
            && double.IsFinite(value);
    }

    public static bool TryParseLatitude(string? text, out double value) =>
        TryParseNumber(text, out value) && value is >= -90 and <= 90;

    public static bool TryParseLongitude(string? text, out double value) =>
        TryParseNumber(text, out value) && value is >= -180 and <= 180;

    /// <summary>Empty means "no elevation".</summary>
    public static bool TryParseElevation(string? text, out double? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (TryParseNumber(text, out var meters) && meters is >= MinElevation and <= MaxElevation)
        {
            value = meters;
            return true;
        }

        return false;
    }

    /// <summary>UTC date and time; empty means "no time". The value is never converted between time zones.</summary>
    public static bool TryParseUtc(string? text, out DateTimeOffset? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (DateTime.TryParseExact(text.Trim(), UtcFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc))
        {
            value = new DateTimeOffset(utc, TimeSpan.Zero);
            return true;
        }

        return false;
    }
}
