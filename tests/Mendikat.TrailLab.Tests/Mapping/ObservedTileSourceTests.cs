using System.Net;
using BruTile;
using Mendikat.TrailLab.Mapping.Providers;
using Mendikat.TrailLab.Mapping.Tiles;

namespace Mendikat.TrailLab.Tests.Mapping;

/// <summary>Tile requests with a fake network (no real tile server is contacted in tests).</summary>
public sealed class ObservedTileSourceTests
{
    private static readonly TileInfo Tile = new() { Index = new TileIndex(505, 376, 10) };

    [Theory]
    [InlineData("ign-es", "https://www.ign.es/wmts/mapa-raster?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=MTN&STYLE=default&TILEMATRIXSET=GoogleMapsCompatible&TILEMATRIX=10&TILEROW=376&TILECOL=505&FORMAT=image/jpeg")]
    [InlineData("esri-imagery", "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/10/376/505")]
    [InlineData("osm", "https://tile.openstreetmap.org/10/505/376.png")]
    public void Urls_are_built_from_the_catalog_templates(string providerId, string expected)
    {
        var source = new ObservedTileSource(MapProviderCatalog.Find(providerId)!);

        Assert.Equal(expected, source.GetUrl(Tile).ToString());
    }

    [Fact]
    public void Server_nodes_replace_the_s_placeholder()
    {
        var source = new ObservedTileSource(MapProviderCatalog.Find("opentopomap")!);

        Assert.Matches(@"^https://[abc]\.tile\.opentopomap\.org/10/505/376\.png$", source.GetUrl(Tile).ToString());
    }

    [Fact]
    public async Task Requests_send_an_identifying_user_agent_and_report_success()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
        using var client = TileHttpClientFactory.Create("1.2.3", handler);
        var source = new ObservedTileSource(MapProviderCatalog.Find("osm")!);
        var loaded = 0;
        source.TileLoaded += (_, _) => loaded++;

        var bytes = await source.GetTileAsync(client, Tile, TestContext.Current.CancellationToken);

        Assert.Equal([1, 2, 3], bytes);
        Assert.Equal(1, loaded);
        Assert.StartsWith("MendikatTrailLab/1.2.3", handler.LastRequest!.Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task Http_errors_are_reported_as_failures()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var client = TileHttpClientFactory.Create("1.0", handler);
        var source = new ObservedTileSource(MapProviderCatalog.Default);
        Exception? failure = null;
        source.TileFailed += (_, ex) => failure = ex;

        await Assert.ThrowsAnyAsync<Exception>(() => source.GetTileAsync(client, Tile, TestContext.Current.CancellationToken));

        Assert.IsType<HttpRequestException>(failure);
    }

    [Fact]
    public async Task Network_errors_and_timeouts_are_reported_as_failures()
    {
        var failures = 0;
        var source = new ObservedTileSource(MapProviderCatalog.Default);
        source.TileFailed += (_, _) => failures++;

        using var offline = TileHttpClientFactory.Create("1.0", new FakeHandler(_ => throw new HttpRequestException("offline")));
        await Assert.ThrowsAnyAsync<Exception>(() => source.GetTileAsync(offline, Tile, TestContext.Current.CancellationToken));

        using var slow = TileHttpClientFactory.Create("1.0", new FakeHandler(_ => null), TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<Exception>(() => source.GetTileAsync(slow, Tile, TestContext.Current.CancellationToken));

        Assert.Equal(2, failures);
    }

    [Fact]
    public async Task Cancellation_by_the_map_is_not_a_failure()
    {
        var failures = 0;
        var source = new ObservedTileSource(MapProviderCatalog.Default);
        source.TileFailed += (_, _) => failures++;
        using var client = TileHttpClientFactory.Create("1.0", new FakeHandler(_ => null));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.GetTileAsync(client, Tile, cancellation.Token));

        Assert.Equal(0, failures);
    }

    /// <summary>Returns the given response; a null response means "never answer" (until cancelled).</summary>
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage?> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var response = respond(request);
            if (response is null)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return response!;
        }
    }
}
