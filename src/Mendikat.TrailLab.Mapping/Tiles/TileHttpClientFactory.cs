using System.Net.Http.Headers;

namespace Mendikat.TrailLab.Mapping.Tiles;

/// <summary>HTTP client for tile requests: identifying User-Agent (MAP-10, OSM tile policy) and a timeout (MAP-07).</summary>
public static class TileHttpClientFactory
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    public static HttpClient Create(string applicationVersion, HttpMessageHandler? handler = null, TimeSpan? timeout = null)
    {
        var client = handler is null ? new HttpClient() : new HttpClient(handler);
        client.Timeout = timeout ?? DefaultTimeout;
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MendikatTrailLab", applicationVersion));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(Windows desktop; +https://mendikat.net)"));
        return client;
    }
}
