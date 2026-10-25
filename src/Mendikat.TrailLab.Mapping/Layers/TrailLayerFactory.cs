using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Styles;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Domain.Model;
using Mendikat.TrailLab.Mapping.Coordinates;
using NetTopologySuite.Geometries;

namespace Mendikat.TrailLab.Mapping.Layers;

/// <summary>Map layers of one document item (a track, a route or the waypoint list), so it can be shown or hidden alone (LAY-01).</summary>
/// <param name="Model">The <see cref="Track"/>, <see cref="Route"/> or waypoint list.</param>
public sealed record ItemLayers(object Model, IReadOnlyList<ILayer> Layers);

/// <summary>
/// Builds the map layers of a document from its <see cref="TrailData"/>. Geographic positions are projected
/// to Web Mercator here, and only here, through <see cref="WebMercator"/> (MAP-08). Point features keep the
/// <see cref="TrailPoint"/> in <see cref="IFeature.Data"/> for selection on the map (LAY-03).
/// </summary>
public static class TrailLayerFactory
{
    public static IReadOnlyList<ItemLayers> CreateLayers(TrailData data, Color color, string name)
    {
        // Styles are shared by all features of the document: large tracks have 100 000+ points (NFR-09).
        var trackLine = MapStyles.TrackLine(color);
        var routeLine = MapStyles.RouteLine(color);
        var trackPoint = MapStyles.TrackPoint(color);
        var waypoint = MapStyles.Waypoint(color);
        var result = new List<ItemLayers>();

        for (var t = 0; t < data.Tracks.Count; t++)
        {
            var track = data.Tracks[t];
            var lines = track.Segments.Select(s => LineFeature(s.Points, trackLine, track)).OfType<IFeature>().ToList();
            var points = track.Segments.SelectMany(s => s.Points).Select(p => (IFeature)PointFeature(p, PointStyle(p, trackPoint))).ToList();
            result.Add(new ItemLayers(track, [Layer($"{name}:track{t}:lines", lines), Layer($"{name}:track{t}:points", points)]));
        }

        for (var r = 0; r < data.Routes.Count; r++)
        {
            var route = data.Routes[r];
            var lines = new[] { LineFeature(route.Points, routeLine, route) }.OfType<IFeature>().ToList();
            var points = route.Points.Select(p => (IFeature)PointFeature(p, PointStyle(p, trackPoint))).ToList();
            result.Add(new ItemLayers(route, [Layer($"{name}:route{r}:lines", lines), Layer($"{name}:route{r}:points", points)]));
        }

        if (data.Waypoints.Count > 0)
        {
            var waypoints = data.Waypoints.Select(w => (IFeature)WaypointFeature(w, waypoint)).ToList();
            result.Add(new ItemLayers(data.Waypoints, [Layer($"{name}:waypoints", waypoints)]));
        }

        return result;
    }

    /// <summary>Highlight of selected points (LAY-02/LAY-03), drawn above everything else.</summary>
    public static IEnumerable<IFeature> SelectionFeatures(IEnumerable<GeoPosition> positions) => Markers(positions, MapStyles.SelectedPoint);

    /// <summary>Markers with the given style at the given positions (selection, profile hover…).</summary>
    public static IEnumerable<IFeature> Markers(IEnumerable<GeoPosition> positions, IStyle style) =>
        positions.Select(p =>
        {
            var feature = new PointFeature(WebMercator.ToWorld(p));
            feature.Styles.Add(style);
            return (IFeature)feature;
        });

    /// <summary>Extent of the data in Web Mercator, or null if there are no points.</summary>
    public static MRect? WorldExtent(GeoBounds? bounds)
    {
        if (bounds is not { } b)
        {
            return null;
        }

        var (minX, minY) = WebMercator.ToWorld(new GeoPosition(b.MinLatitude, b.MinLongitude));
        var (maxX, maxY) = WebMercator.ToWorld(new GeoPosition(b.MaxLatitude, b.MaxLongitude));
        return new MRect(minX, minY, maxX, maxY);
    }

    private static IStyle PointStyle(TrailPoint point, IStyle normal) => point.IsAdded ? MapStyles.AddedPoint : normal;

    private static MemoryLayer Layer(string name, List<IFeature> features) =>
        new(name) { Features = features, Style = null };

    private static GeometryFeature? LineFeature(List<TrailPoint> points, IStyle style, object owner)
    {
        if (points.Count < 2)
        {
            return null; // a single point is drawn by the points layer
        }

        var coordinates = points.Select(p =>
        {
            var (x, y) = WebMercator.ToWorld(p.Position);
            return new Coordinate(x, y);
        }).ToArray();
        var feature = new GeometryFeature(new LineString(coordinates)) { Data = owner };
        feature.Styles.Add(style);
        return feature;
    }

    private static PointFeature PointFeature(TrailPoint point, IStyle style)
    {
        var feature = new PointFeature(WebMercator.ToWorld(point.Position)) { Data = point };
        feature.Styles.Add(style);
        return feature;
    }

    private static PointFeature WaypointFeature(TrailPoint point, IStyle symbol)
    {
        var feature = PointFeature(point, symbol);
        if (!string.IsNullOrWhiteSpace(point.Name))
        {
            feature.Styles.Add(MapStyles.WaypointLabel(point.Name));
        }

        return feature;
    }
}
