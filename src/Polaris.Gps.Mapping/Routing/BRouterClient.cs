using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Polaris.Gps.Domain.Geo;

namespace Polaris.Gps.Mapping.Routing;

/// <summary>A point of a computed route: position and, when the service gives it, terrain elevation in meters.</summary>
public sealed record RoutedPoint(GeoPosition Position, double? Elevation);

/// <summary>Finds the way between two points along paths and roads (drawing with "Follow paths").</summary>
public interface IRouter
{
    /// <summary>Name and licence of the data, shown to the user.</summary>
    string Attribution { get; }

    /// <summary>The route from <paramref name="from"/> to <paramref name="to"/>, both included, for a routing profile.</summary>
    /// <exception cref="RoutingException">No route was found or the service could not be reached.</exception>
    Task<IReadOnlyList<RoutedPoint>> RouteAsync(GeoPosition from, GeoPosition to, string profile, CancellationToken cancellationToken);
}

/// <summary>The route could not be computed; nothing has been changed.</summary>
/// <param name="noRoute">True when the service answered but found no way (no path nearby, unconnected areas).</param>
public sealed class RoutingException(string message, bool noRoute, Exception? innerException = null) : Exception(message, innerException)
{
    public bool NoRoute { get; } = noRoute;
}

/// <summary>A routing profile of BRouter: its id on the server and what it is for.</summary>
public sealed record RoutingProfile(string Id, RoutingActivity Activity);

/// <summary>What a routing profile is meant for; the application names it in the user's language.</summary>
public enum RoutingActivity
{
    MountainHiking,
    Trekking,
    MountainBike,
    RoadBike,
    Shortest,
}

/// <summary>
/// Routes with BRouter (https://brouter.de, MIT licence, OpenStreetMap data): GET
/// <c>{server}?lonlats=lon,lat|lon,lat&amp;profile=…&amp;alternativeidx=0&amp;format=geojson</c>. The answer is a GeoJSON
/// FeatureCollection whose LineString coordinates are [lon, lat, elevation]. When there is no route the server answers
/// with an error status and a plain text message. Checked against brouter.de on 2026-10-30.
/// </summary>
public sealed class BRouterClient(HttpClient http, string server = BRouterClient.PublicServer) : IRouter
{
    public const string PublicServer = "https://brouter.de/brouter";

    /// <summary>The profiles offered, mountain hiking first (the default).</summary>
    public static IReadOnlyList<RoutingProfile> Profiles { get; } =
    [
        new("hiking-mountain", RoutingActivity.MountainHiking),
        new("trekking", RoutingActivity.Trekking),
        new("mtb", RoutingActivity.MountainBike),
        new("fastbike", RoutingActivity.RoadBike),
        new("shortest", RoutingActivity.Shortest),
    ];

    public string Attribution => "BRouter (brouter.de) · © OpenStreetMap contributors (ODbL)";

    public async Task<IReadOnlyList<RoutedPoint>> RouteAsync(GeoPosition from, GeoPosition to, string profile, CancellationToken cancellationToken)
    {
        var url = string.Create(CultureInfo.InvariantCulture,
            $"{server}?lonlats={from.Longitude:0.######},{from.Latitude:0.######}%7C{to.Longitude:0.######},{to.Latitude:0.######}&profile={Uri.EscapeDataString(profile)}&alternativeidx=0&format=geojson");
        string body;
        try
        {
            using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new RoutingException(FirstLine(body), noRoute: (int)response.StatusCode < 500 || body.Length > 0);
            }
        }
        catch (HttpRequestException ex)
        {
            throw new RoutingException(ex.Message, noRoute: false, ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new RoutingException("timeout", noRoute: false, ex);
        }

        return Parse(body);
    }

    /// <summary>The points of a BRouter GeoJSON answer.</summary>
    /// <exception cref="RoutingException">The text is not a route.</exception>
    public static IReadOnlyList<RoutedPoint> Parse(string geoJson)
    {
        try
        {
            using var document = JsonDocument.Parse(geoJson);
            var coordinates = document.RootElement.GetProperty("features")[0].GetProperty("geometry").GetProperty("coordinates");
            var points = new List<RoutedPoint>(coordinates.GetArrayLength());
            foreach (var coordinate in coordinates.EnumerateArray())
            {
                var lon = coordinate[0].GetDouble();
                var lat = coordinate[1].GetDouble();
                double? elevation = coordinate.GetArrayLength() > 2 ? coordinate[2].GetDouble() : null;
                points.Add(new RoutedPoint(new GeoPosition(lat, lon), elevation));
            }

            return points.Count >= 2 ? points : throw new RoutingException("empty route", noRoute: true);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException or ArgumentException)
        {
            throw new RoutingException(FirstLine(geoJson), noRoute: true, ex);
        }
    }

    private static string FirstLine(string text)
    {
        var line = text.Split('\n', 2)[0].Trim();
        return line.Length > 200 ? line[..200] : line;
    }
}
