namespace Mendikat.TrailLab.Mapping.Providers;

/// <summary>
/// A raster tile source in Web Mercator (EPSG:3857, "Google" tiling, y from the top).
/// <see cref="UrlTemplate"/> uses the BruTile placeholders {x}, {y}, {z} and {s} (server node).
/// </summary>
public sealed record MapProvider(
    string Id,
    string DisplayName,
    string UrlTemplate,
    string AttributionText,
    string AttributionUrl,
    int MinZoom = 0,
    int MaxZoom = 19,
    IReadOnlyList<string>? ServerNodes = null,
    string? UsageNote = null)
{
    public override string ToString() => DisplayName;
}
