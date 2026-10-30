namespace Polaris.Gps.Domain.Geo;

/// <summary>
/// Geographic position on WGS84 (EPSG:4326), in decimal degrees. Latitude always comes first and is named
/// explicitly to avoid lat/lon swaps (ADR-0003). Projected or screen coordinates never use this type.
/// </summary>
public readonly record struct GeoPosition
{
    public GeoPosition(double latitude, double longitude)
    {
        if (!IsValidLatitude(latitude))
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude must be within [-90, 90].");
        }

        if (!IsValidLongitude(longitude))
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude must be within [-180, 180].");
        }

        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; }

    public double Longitude { get; }

    public static bool IsValidLatitude(double value) => double.IsFinite(value) && value is >= -90 and <= 90;

    public static bool IsValidLongitude(double value) => double.IsFinite(value) && value is >= -180 and <= 180;

    public override string ToString() =>
        FormattableString.Invariant($"({Latitude:0.0000000}, {Longitude:0.0000000})");
}
