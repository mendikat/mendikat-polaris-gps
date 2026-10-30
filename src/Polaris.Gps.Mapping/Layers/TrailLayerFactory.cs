using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Styles;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Mapping.Coordinates;
using NetTopologySuite.Geometries;

namespace Polaris.Gps.Mapping.Layers;

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
    /// <param name="coloring">Colours the track lines by elevation, slope or speed (View › Colour tracks by); routes keep <paramref name="color"/>.</param>
    public static IReadOnlyList<ItemLayers> CreateLayers(TrailData data, Color color, string name, TrackColorMode coloring = TrackColorMode.None)
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
            var lines = TrackColoring.Classify(track, coloring) is { } classes
                ? ColoredLines(track, classes, coloring, trackLine)
                : track.Segments.Select(s => LineFeature(s.Points, trackLine, track)).OfType<IFeature>().ToList();
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
            var limits = LabelLimits(data.Waypoints);
            var waypoints = data.Waypoints
                .Select((w, i) => (IFeature)WaypointFeature(w, WaypointSymbols.Icon(WaypointSymbols.KindOf(w.Symbol)) ?? waypoint, limits[i]))
                .ToList();
            result.Add(new ItemLayers(data.Waypoints, [Layer($"{name}:waypoints", waypoints)]));
        }

        return result;
    }

    /// <summary>Highlight of selected points (LAY-02/LAY-03), drawn above everything else.</summary>
    public static IEnumerable<IFeature> SelectionFeatures(IEnumerable<GeoPosition> positions) =>
        positions.Select(p =>
        {
            var feature = new PointFeature(WebMercator.ToWorld(p));
            feature.Styles.Add(MapStyles.SelectedPointEdge);
            feature.Styles.Add(MapStyles.SelectedPoint);
            return (IFeature)feature;
        });

    /// <summary>Markers with the given style at the given positions (selection, profile hover…).</summary>
    public static IEnumerable<IFeature> Markers(IEnumerable<GeoPosition> positions, IStyle style) =>
        positions.Select(p =>
        {
            var feature = new PointFeature(WebMercator.ToWorld(p));
            feature.Styles.Add(style);
            return (IFeature)feature;
        });

    /// <summary>
    /// Features of the UTM grid: each line, and its label at the first sample inside <paramref name="view"/> (bottom
    /// of the easting lines, left of the northing lines).
    /// </summary>
    public static IEnumerable<IFeature> GridFeatures(IReadOnlyList<GridLine> lines, GeoBounds view)
    {
        foreach (var line in lines)
        {
            var coordinates = line.Points.Select(p =>
            {
                var (x, y) = WebMercator.ToWorld(p);
                return new Coordinate(x, y);
            }).ToArray();
            var feature = new GeometryFeature(new LineString(coordinates));
            feature.Styles.Add(MapStyles.GridLine);
            yield return feature;

            var anchor = line.Points.Skip(1).FirstOrDefault(view.Contains);
            if (anchor != default)
            {
                var label = new PointFeature(WebMercator.ToWorld(anchor));
                label.Styles.Add(MapStyles.GridLabel(line.Label, line.IsEasting));
                yield return label;
            }
        }
    }

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

    /// <summary>
    /// The lines of a track coloured by class: one feature per run of consecutive points of the same class (a run shares
    /// its last point with the next one, so the line has no gaps); points without a value use the document colour.
    /// </summary>
    private static List<IFeature> ColoredLines(Track track, int[] classes, TrackColorMode mode, IStyle plain)
    {
        var styles = Enumerable.Range(0, TrackColoring.ClassCount(mode))
            .Select(i => TrackColoring.ClassColor(mode, i))
            .Select(c => MapStyles.ColoredTrackLine(new Color(c.R, c.G, c.B)))
            .ToArray();
        var lines = new List<IFeature>();
        var offset = 0;
        foreach (var segment in track.Segments)
        {
            var points = segment.Points;
            var start = 0;
            while (start < points.Count - 1)
            {
                var current = classes[offset + start];
                var end = start + 1;
                while (end < points.Count - 1 && classes[offset + end] == current)
                {
                    end++;
                }

                if (LineFeature(points.GetRange(start, end - start + 1), current < 0 ? plain : styles[current], track) is { } line)
                {
                    lines.Add(line);
                }

                start = end;
            }

            offset += points.Count;
        }

        return lines;
    }

    /// <summary>Approximate size in pixels of a waypoint label: 7 px per character plus the padding, 18 px high.</summary>
    private static double LabelWidth(string? name) => (name?.Length ?? 0) * 7 + 12;

    private const double LabelHeight = 18;

    /// <summary>Above this many waypoints the labels are not decluttered (the check compares every pair).</summary>
    public const int MaxDeclutteredWaypoints = 3000;

    /// <summary>
    /// The coarsest resolution (meters per pixel) at which each waypoint label is drawn, so that labels do not overlap:
    /// a label is hidden while it would overlap the label of an earlier waypoint, and shows as the map zooms in and they
    /// separate. Earlier waypoints win. Two labels separate below the resolution where their distance in pixels exceeds
    /// half their widths together (horizontally) or the label height (vertically).
    /// </summary>
    public static double[] LabelLimits(IReadOnlyList<TrailPoint> waypoints)
    {
        var limits = Enumerable.Repeat(MapStyles.PointsMaxResolution * 10, waypoints.Count).ToArray();
        if (waypoints.Count > MaxDeclutteredWaypoints)
        {
            return limits;
        }

        var world = waypoints.Select(w => WebMercator.ToWorld(w.Position)).ToArray();
        for (var i = 0; i < waypoints.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(waypoints[i].Name))
            {
                continue;
            }

            for (var j = 0; j < i; j++)
            {
                if (string.IsNullOrWhiteSpace(waypoints[j].Name))
                {
                    continue;
                }

                var dx = Math.Abs(world[i].X - world[j].X);
                var dy = Math.Abs(world[i].Y - world[j].Y);
                var separate = Math.Max(dx / ((LabelWidth(waypoints[i].Name) + LabelWidth(waypoints[j].Name)) / 2), dy / LabelHeight);
                limits[i] = Math.Min(limits[i], separate);
            }
        }

        return limits;
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

    private static PointFeature WaypointFeature(TrailPoint point, IStyle symbol, double labelMaxResolution)
    {
        var feature = PointFeature(point, symbol);
        if (!string.IsNullOrWhiteSpace(point.Name))
        {
            feature.Styles.Add(MapStyles.WaypointLabel(point.Name, labelMaxResolution));
        }

        return feature;
    }
}
