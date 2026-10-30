namespace Polaris.Gps.Domain.Geo;

/// <summary>Latitude/longitude bounding box (WGS84). Does not handle the antimeridian specially.</summary>
public readonly record struct GeoBounds(double MinLatitude, double MinLongitude, double MaxLatitude, double MaxLongitude)
{
    public static GeoBounds? FromPositions(IEnumerable<GeoPosition> positions)
    {
        GeoBounds? bounds = null;
        foreach (var p in positions)
        {
            bounds = bounds is { } b ? b.Include(p) : new GeoBounds(p.Latitude, p.Longitude, p.Latitude, p.Longitude);
        }

        return bounds;
    }

    public GeoBounds Include(GeoPosition p) => new(
        Math.Min(MinLatitude, p.Latitude),
        Math.Min(MinLongitude, p.Longitude),
        Math.Max(MaxLatitude, p.Latitude),
        Math.Max(MaxLongitude, p.Longitude));

    /// <summary>True if <paramref name="p"/> is inside the box or on its border.</summary>
    public bool Contains(GeoPosition p) =>
        p.Latitude >= MinLatitude && p.Latitude <= MaxLatitude && p.Longitude >= MinLongitude && p.Longitude <= MaxLongitude;

    public GeoBounds Union(GeoBounds other) => new(
        Math.Min(MinLatitude, other.MinLatitude),
        Math.Min(MinLongitude, other.MinLongitude),
        Math.Max(MaxLatitude, other.MaxLatitude),
        Math.Max(MaxLongitude, other.MaxLongitude));
}
