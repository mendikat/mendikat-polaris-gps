using BruTile;
using BruTile.Predefined;
using BruTile.Web;
using Polaris.Gps.Mapping.Providers;

namespace Polaris.Gps.Mapping.Tiles;

/// <summary>
/// Tile source for a <see cref="MapProvider"/> that reports successes and failures, so the UI can tell the
/// user when the cartography cannot be loaded (MAP-07). Requests run on background threads; events too.
/// </summary>
public sealed class ObservedTileSource : IHttpTileSource
{
    private readonly HttpTileSource _inner;

    public ObservedTileSource(MapProvider provider)
    {
        Provider = provider;
        var schema = new GlobalSphericalMercator(provider.MinZoom, provider.MaxZoom, provider.Id);
        // No attribution here on purpose: Mapsui would draw its own copy over the map. The application
        // shows the provider attribution itself, as a link, from MapProvider (MAP-06).
        _inner = new HttpTileSource(schema, new BasicUrlBuilder(provider.UrlTemplate, provider.ServerNodes), name: provider.Id);
    }

    /// <summary>A tile was downloaded.</summary>
    public event EventHandler? TileLoaded;

    /// <summary>A tile could not be downloaded (network error, HTTP error status or timeout).</summary>
    public event EventHandler<Exception>? TileFailed;

    public MapProvider Provider { get; }

    public ITileSchema Schema => _inner.Schema;

    public string Name => _inner.Name;

    public Attribution Attribution => _inner.Attribution;

    public Uri GetUrl(TileInfo tileInfo) => _inner.GetUrl(tileInfo);

    public async Task<byte[]?> GetTileAsync(HttpClient httpClient, TileInfo tileInfo, CancellationToken? cancellation)
    {
        try
        {
            var bytes = await _inner.GetTileAsync(httpClient, tileInfo, cancellation).ConfigureAwait(false);
            TileLoaded?.Invoke(this, EventArgs.Empty);
            return bytes;
        }
        catch (OperationCanceledException) when (cancellation?.IsCancellationRequested == true)
        {
            throw; // the map no longer needs this tile: not a failure
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
        {
            // OperationCanceledException without our cancellation is the HttpClient timeout.
            TileFailed?.Invoke(this, ex);
            throw;
        }
    }
}
