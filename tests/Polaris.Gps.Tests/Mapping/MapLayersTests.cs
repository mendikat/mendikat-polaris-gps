using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Gpx;
using Polaris.Gps.Mapping.Coordinates;
using Polaris.Gps.Mapping.Layers;

namespace Polaris.Gps.Tests.Mapping;

public sealed class MapLayersTests
{
    /// <summary>Half the circumference of the Web Mercator sphere (EPSG:3857 x at 180°).</summary>
    private const double HalfWorld = 20037508.342789244;

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(0, 180, HalfWorld, 0)]
    [InlineData(0, -90, -HalfWorld / 2, 0)]
    public void Web_mercator_projection_matches_epsg_3857(double lat, double lon, double x, double y)
    {
        var (wx, wy) = WebMercator.ToWorld(new GeoPosition(lat, lon));

        Assert.Equal(x, wx, 0.01);
        Assert.Equal(y, wy, 0.01);
    }

    [Fact]
    public void Web_mercator_round_trip_is_below_a_centimeter()
    {
        var original = new GeoPosition(42.9853, -2.0786);

        var (x, y) = WebMercator.ToWorld(original);
        var back = WebMercator.ToGeo(x, y);

        // 1e-7 degrees is about 1 cm.
        Assert.Equal(original.Latitude, back.Latitude, 1e-7);
        Assert.Equal(original.Longitude, back.Longitude, 1e-7);
    }

    [Fact]
    public void Pointer_beyond_the_antimeridian_wraps_longitude()
    {
        var position = WebMercator.ToGeo(HalfWorld * 1.5, 0);

        Assert.Equal(-90, position.Longitude, 1e-9);
    }

    [Fact]
    public void Each_item_gets_its_own_layers_so_it_can_be_hidden_alone()
    {
        TrailData data;
        using (var stream = Fixture.Open("gpx/valid-11.gpx"))
        {
            data = new GpxReader().Read(stream, TestContext.Current.CancellationToken).Data;
        }

        var items = TrailLayerFactory.CreateLayers(data, MapStyles.ColorFor(0), "valid");

        Assert.Equal(3, items.Count); // track, route, waypoints
        Assert.Same(data.Tracks[0], items[0].Model);
        Assert.Same(data.Routes[0], items[1].Model);
        Assert.Same(data.Waypoints, items[2].Model);
        var trackLayers = items[0].Layers.Cast<MemoryLayer>().ToList();
        Assert.Equal(2, trackLayers[0].Features.Count());  // one line per segment
        Assert.Equal(5, trackLayers[1].Features.Count());  // track points
        Assert.All(trackLayers[0].Features, f => Assert.IsType<GeometryFeature>(f));
        var waypoints = ((MemoryLayer)items[2].Layers.Single()).Features.ToList();
        Assert.Same(data.Waypoints[0], waypoints[0].Data);
    }

    [Fact]
    public void A_gpx_with_only_waypoints_shows_each_one_with_its_symbol_and_name()
    {
        // Like the 890 summits of mendikat.net's waypoints.gpx: no tracks or routes.
        const string gpx = """
            <gpx version="1.1" creator="Mendikat" xmlns="http://www.topografix.com/GPX/1/1">
              <wpt lat="42.714503443806" lon="-4.0036630710306"><ele>1047</ele><name>Adrero (1047 m)</name><sym>summit</sym></wpt>
              <wpt lat="42.047284334594" lon="-3.0413745024086"><ele>1909</ele><name>Aguda, Peña (1909 m)</name><sym>summit</sym></wpt>
              <wpt lat="42.1" lon="-3.5"><ele>1000</ele></wpt>
            </gpx>
            """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(gpx));
        var data = new GpxReader().Read(stream, TestContext.Current.CancellationToken).Data;

        var item = Assert.Single(TrailLayerFactory.CreateLayers(data, MapStyles.ColorFor(0), "waypoints"));

        Assert.Same(data.Waypoints, item.Model);
        var features = ((MemoryLayer)item.Layers.Single()).Features.ToList();
        Assert.Equal(3, features.Count);
        // "summit" (any case) has its own icon; a waypoint without symbol keeps the triangle of the document colour
        Assert.All(features.Take(2), f => Assert.IsType<Mapsui.Styles.ImageStyle>(f.Styles.First()));
        Assert.IsType<Mapsui.Styles.SymbolStyle>(features[2].Styles.First());
        Assert.Equal("Aguda, Peña (1909 m)", ((Mapsui.Styles.LabelStyle)features[1].Styles.Last()).GetLabelText(features[1]));
        Assert.Single(features[2].Styles); // without a name there is no label
    }

    [Fact]
    public void Waypoint_symbols_are_matched_by_name_and_synonyms()
    {
        Assert.Equal(WaypointKind.Summit, WaypointSymbols.KindOf("SUMMIT"));
        Assert.Equal(WaypointKind.Shelter, WaypointSymbols.KindOf("Hut"));
        Assert.Equal(WaypointKind.Generic, WaypointSymbols.KindOf("Flag, Blue"));
        Assert.Null(WaypointSymbols.Icon(WaypointKind.Generic));
        Assert.All(WaypointSymbols.All.Skip(1), s => Assert.StartsWith("<svg", WaypointSymbols.Svg(s.Kind)));
    }

    [Fact]
    public void Overlapping_labels_show_only_when_zoomed_in_enough_to_separate()
    {
        // Two named waypoints 100 m apart east-west, a third far away.
        TrailPoint W(double lat, double lon, string? name) => new(new GeoPosition(lat, lon)) { Name = name };
        var waypoints = new List<TrailPoint> { W(43, -2, "Aitzgorri"), W(43, -1.99877, "Aketegi"), W(44, -2, "Lejos"), W(43, -1.9995, null) };

        var limits = TrailLayerFactory.LabelLimits(waypoints);

        Assert.Equal(MapStyles.PointsMaxResolution * 10, limits[0]); // the first one always wins
        Assert.InRange(limits[1], 1, 5); // ~137 m in Web Mercator over labels ~68 px wide: about 2 m/px
        Assert.Equal(MapStyles.PointsMaxResolution * 10, limits[2]);
    }

    [Fact]
    public void Added_points_use_their_own_symbol()
    {
        var data = new TrailData();
        var segment = new TrackSegment();
        segment.Points.Add(new TrailPoint(new GeoPosition(42, -1)));
        segment.Points.Add(new TrailPoint(new GeoPosition(42.1, -1)) { IsAdded = true });
        var track = new Track();
        track.Segments.Add(segment);
        data.Tracks.Add(track);

        var points = ((MemoryLayer)TrailLayerFactory.CreateLayers(data, MapStyles.ColorFor(0), "t")[0].Layers[1]).Features.ToList();

        Assert.NotSame(MapStyles.AddedPoint, points[0].Styles.Single());
        Assert.Same(MapStyles.AddedPoint, points[1].Styles.Single());
    }

    [Fact]
    public void Hit_test_finds_the_nearest_visible_point_within_the_tolerance()
    {
        var data = new TrailData();
        var near = new TrailPoint(new GeoPosition(42, -1));
        var far = new TrailPoint(new GeoPosition(42.001, -1));
        data.Waypoints.AddRange([near, far]);
        var layers = TrailLayerFactory.CreateLayers(data, MapStyles.ColorFor(0), "w").SelectMany(i => i.Layers).ToList();
        var (x, y) = WebMercator.ToWorld(near.Position);

        // 5 m away, resolution 1 m/px, tolerance 8 px → hit.
        var hit = PointHitTester.FindNearest(layers, new MPoint(x + 5, y), resolution: 1);
        Assert.Same(near, hit?.Point);

        // 20 m away → no hit.
        Assert.Null(PointHitTester.FindNearest(layers, new MPoint(x + 20, y), resolution: 1));

        // Hidden layers are ignored.
        layers.ForEach(l => l.Enabled = false);
        Assert.Null(PointHitTester.FindNearest(layers, new MPoint(x + 5, y), resolution: 1));
    }

    [Fact]
    public void World_extent_covers_the_data_bounds()
    {
        var extent = TrailLayerFactory.WorldExtent(new GeoBounds(42.98, -2.08, 42.99, -2.06))!;
        var (x, y) = WebMercator.ToWorld(new GeoPosition(42.985, -2.07));

        Assert.True(extent.Contains(new MPoint(x, y)));
        Assert.Null(TrailLayerFactory.WorldExtent(null));
    }
}
