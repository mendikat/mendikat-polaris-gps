using System.Globalization;
using Mendikat.TrailLab.Domain.Elevation;
using Mendikat.TrailLab.Domain.Geo;

namespace Mendikat.TrailLab.Mapping.Elevation;

/// <summary>Resolution of the IGN digital terrain model.</summary>
public enum IgnDemResolution
{
    /// <summary>MDT05, 5 m grid (coverage Elevacion4258_5).</summary>
    FiveMeters,

    /// <summary>MDT25, 25 m grid (coverage Elevacion4258_25).</summary>
    TwentyFiveMeters,
}

/// <summary>
/// Elevations from the IGN "Modelos Digitales del Terreno de España" WCS (ADR-0007), CC BY 4.0, Spain only.
/// Behaviour verified against the service on 2026-10-25:
/// <list type="bullet">
/// <item>GetCoverage with <c>FORMAT=application/asc</c> returns a multipart response holding an ArcGrid, resampled to the request.</item>
/// <item>There is no NODATA value: the sea and areas outside Spain come back as exactly 0. Exact zeros are therefore
/// treated as "no data" (a point at exactly 0 m keeps its elevation, which is the safe outcome).</item>
/// <item>A request outside the coverage extent returns an OWS ExceptionReport ("ExtentError"): all its points have no data.</item>
/// </list>
/// Points are grouped into small tiles (0.005°, ~0.5 km) so a long track downloads only the area around it.
/// </summary>
public sealed class IgnWcsElevationSource : IElevationSource
{
    public const string Endpoint = "https://servicios.idee.es/wcs-inspire/mdt";

    /// <summary>Tile size in degrees; each tile is one request.</summary>
    public const double TileDegrees = 0.005;

    private const int MaxParallelRequests = 4;

    /// <summary>Downloaded tiles kept in memory (about 50 KB each at 5 m); the cache is emptied when it is full.</summary>
    public const int MaxCachedTiles = 256;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<(int Lat, int Lon), ArcGrid?> _tiles = new();

    private readonly HttpClient _http;
    private readonly IgnDemResolution _resolution;

    public IgnWcsElevationSource(HttpClient http, IgnDemResolution resolution = IgnDemResolution.FiveMeters)
    {
        _http = http;
        _resolution = resolution;
    }

    public string Name => _resolution == IgnDemResolution.FiveMeters ? "MDT IGN 5 m" : "MDT IGN 25 m";

    public string VerticalDatum =>
        "Altitudes ortométricas sobre el nivel medio del mar (Alicante en la Península; nivel medio local en Baleares y Canarias), " +
        "red REDNAP. Sin corrección geoidal adicional.";

    public string Attribution => "MDT © Instituto Geográfico Nacional de España (CC BY 4.0 scne.es)";

    private string CoverageId => _resolution == IgnDemResolution.FiveMeters ? "Elevacion4258_5" : "Elevacion4258_25";

    /// <summary>Cell size of the coverage in degrees, used as the margin around each tile.</summary>
    private double Margin => _resolution == IgnDemResolution.FiveMeters ? 0.0001 : 0.0005;

    public async Task<IReadOnlyList<double?>> GetElevationsAsync(
        IReadOnlyList<GeoPosition> positions, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var results = new double?[positions.Count];
        var tiles = positions
            .Select((p, i) => (Key: (Lat: (int)Math.Floor(p.Latitude / TileDegrees), Lon: (int)Math.Floor(p.Longitude / TileDegrees)), Index: i))
            .GroupBy(x => x.Key)
            .ToList();

        var done = 0;
        using var throttle = new SemaphoreSlim(MaxParallelRequests);
        var tasks = tiles.Select(async tile =>
        {
            await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var grid = await GetTileAsync(tile.Key.Lat, tile.Key.Lon, cancellationToken).ConfigureAwait(false);
                foreach (var (_, index) in tile)
                {
                    results[index] = grid?.Interpolate(positions[index].Latitude, positions[index].Longitude, IsNoData);
                }
            }
            finally
            {
                throttle.Release();
                progress?.Report((double)Interlocked.Increment(ref done) / tiles.Count);
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return results;
    }

    /// <summary>The service has no NODATA value; exact zeros are the sea or outside Spain.</summary>
    public static bool IsNoData(double value) => value == 0;

    public Uri TileUrl(int latIndex, int lonIndex)
    {
        var south = latIndex * TileDegrees - Margin;
        var west = lonIndex * TileDegrees - Margin;
        var north = (latIndex + 1) * TileDegrees + Margin;
        var east = (lonIndex + 1) * TileDegrees + Margin;
        return new Uri(string.Create(CultureInfo.InvariantCulture,
            $"{Endpoint}?SERVICE=WCS&VERSION=2.0.1&REQUEST=GetCoverage&COVERAGEID={CoverageId}" +
            $"&SUBSET=Lat({south:0.######},{north:0.######})&SUBSET=Long({west:0.######},{east:0.######})&FORMAT=application/asc"));
    }

    /// <summary>
    /// A tile from the cache or the service. Tiles already downloaded by this source are reused, so the status bar
    /// (pointer elevation) and repeated adjustments of the same area do not download them again. Failures are not cached.
    /// </summary>
    private async Task<ArcGrid?> GetTileAsync(int latIndex, int lonIndex, CancellationToken cancellationToken)
    {
        if (_tiles.TryGetValue((latIndex, lonIndex), out var cached))
        {
            return cached;
        }

        var grid = await FetchTileAsync(latIndex, lonIndex, cancellationToken).ConfigureAwait(false);
        if (_tiles.Count >= MaxCachedTiles)
        {
            _tiles.Clear();
        }

        _tiles[(latIndex, lonIndex)] = grid;
        return grid;
    }

    /// <returns>The grid, or null if the tile is outside the coverage.</returns>
    private async Task<ArcGrid?> FetchTileAsync(int latIndex, int lonIndex, CancellationToken cancellationToken)
    {
        string text;
        try
        {
            using var response = await _http.GetAsync(TileUrl(latIndex, lonIndex), cancellationToken).ConfigureAwait(false);
            text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode && !text.Contains("ExceptionReport", StringComparison.Ordinal))
            {
                throw new ElevationSourceException($"HTTP {(int)response.StatusCode} from the IGN elevation service.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
        {
            throw new ElevationSourceException("The IGN elevation service could not be reached.", ex);
        }

        if (text.Contains("ExtentError", StringComparison.Ordinal))
        {
            return null; // outside the coverage: no data for these points
        }

        if (text.Contains("ExceptionReport", StringComparison.Ordinal))
        {
            throw new ElevationSourceException("The IGN elevation service returned an error.");
        }

        try
        {
            return ArcGrid.Parse(text);
        }
        catch (FormatException ex)
        {
            throw new ElevationSourceException("Unexpected response from the IGN elevation service.", ex);
        }
    }
}
