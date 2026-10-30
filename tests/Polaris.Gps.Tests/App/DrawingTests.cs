using Polaris.Gps.App.ViewModels;
using Polaris.Gps.Domain.Elevation;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Domain.Processing;
using Polaris.Gps.Mapping.Routing;
using Polaris.Gps.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Polaris.Gps.Tests.App;

/// <summary>Drawing mode, follow paths, elevation of the new points, snapping, loop and out-and-back, live figures.</summary>
public sealed class DrawingTests
{
    private static readonly GeoPosition A = new(43.0, -2.0);
    private static readonly GeoPosition B = new(43.01, -2.0);

    private readonly FakeDialogService _dialogs = new();
    private readonly FakeSettingsStore _settings = new();
    private readonly FakeRouter _router = new();
    private readonly FakeTerrain _terrain = new();
    private readonly MainViewModel _vm;

    public DrawingTests()
    {
        _vm = new MainViewModel(_dialogs, new FakeDocumentStore(), _settings, NullLogger<MainViewModel>.Instance)
        {
            Router = _router,
            ElevationSources = _ => _terrain,
        };
    }

    private Track Track => _vm.ActiveDocument!.Document.Data.Tracks[^1];

    [Fact]
    public void Drawing_mode_opens_a_document_if_needed_shows_figures_and_ends_with_escape()
    {
        _vm.IsDrawing = true;

        Assert.NotNull(_vm.ActiveDocument);
        Assert.True(_vm.CancelOperationCommand.CanExecute(null));
        Assert.Equal("✎ 0,00 km · +0 m / −0 m · ≈ — (MIDE)", _vm.DrawingStats);

        _vm.CancelOperation();

        Assert.False(_vm.IsDrawing);
        Assert.Null(_vm.DrawingStats);
    }

    [Fact]
    public async Task New_points_take_the_terrain_elevation_and_the_figures_follow()
    {
        _vm.IsDrawing = true;
        _terrain.Value = 1000;
        await _vm.AddPointAtAsync(A);
        _terrain.Value = 1200;
        await _vm.AddPointAtAsync(B);

        Assert.Equal([1000.0, 1200.0], Track.AllPoints.Select(p => p.Elevation!.Value));
        Assert.All(Track.AllPoints, p => Assert.Equal(ElevationSource.Dem, p.ElevationSource));
        Assert.StartsWith("✎ 1,11 km · +", _vm.DrawingStats);
    }

    [Fact]
    public async Task Without_automatic_elevation_or_offline_points_have_none()
    {
        _vm.NewGpx();
        _vm.AutoElevation = false;
        await _vm.AddPointAtAsync(A);
        _vm.AutoElevation = true;
        _terrain.Fail = true;
        await _vm.AddPointAtAsync(B);

        Assert.All(Track.AllPoints, p => Assert.Null(p.Elevation));
        Assert.True(_settings.Current.Drawing.AutoElevation); // saved each time it changes
    }

    [Fact]
    public async Task Following_paths_inserts_the_routed_stretch_after_the_previous_point_with_its_elevations()
    {
        _vm.NewGpx();
        _vm.AutoElevation = false;
        _vm.FollowPaths = true;
        await _vm.AddPointAtAsync(A); // first point: nothing to route from
        _router.Way = [new(A, 900), new(new GeoPosition(43.005, -2.001), 950), new(B, 1000)];

        await _vm.AddPointAtAsync(B);

        Assert.Equal(3, Track.PointCount);
        Assert.Equal([A, new GeoPosition(43.005, -2.001), B], Track.AllPoints.Select(p => p.Position));
        Assert.Equal([950.0, 1000.0], Track.AllPoints.Skip(1).Select(p => p.Elevation!.Value));
        Assert.Equal("hiking-mountain", _router.LastProfile);
        Assert.True(_settings.Current.Drawing.FollowPaths);
        Assert.Same(Track.AllPoints.Last(), _vm.ActiveDocument!.SelectedRows[0].Point);

        _vm.Undo();
        Assert.Equal(1, Track.PointCount);
    }

    [Fact]
    public async Task Without_a_route_nothing_is_added_and_the_status_says_why()
    {
        _vm.NewGpx();
        _vm.FollowPaths = true;
        await _vm.AddPointAtAsync(A);
        _router.Error = new RoutingException("target island detected", noRoute: true);

        await _vm.AddPointAtAsync(B);

        Assert.Equal(1, Track.PointCount);
        Assert.StartsWith("No se encontró un camino", _vm.StatusMessage);
    }

    [Fact]
    public async Task A_click_near_an_existing_point_snaps_to_it()
    {
        _vm.NewGpx();
        var existing = new TrailPoint(B) { Elevation = 777 };
        _vm.PointAt = p => p == new GeoPosition(43.0101, -2.0) ? existing : null;
        await _vm.AddPointAtAsync(A);

        await _vm.AddPointAtAsync(new GeoPosition(43.0101, -2.0));

        var snapped = Track.AllPoints.Last();
        Assert.Equal(B, snapped.Position);
        Assert.Equal(777, snapped.Elevation);
        Assert.NotSame(existing, snapped);
    }

    [Fact]
    public async Task Closing_the_loop_and_out_and_back_return_to_the_start()
    {
        _vm.NewGpx();
        _vm.AutoElevation = false;
        await _vm.AddPointAtAsync(A);
        await _vm.AddPointAtAsync(B);

        await _vm.CloseLoopAsync();
        Assert.Equal([A, B, A], Track.AllPoints.Select(p => p.Position));
        await _vm.CloseLoopAsync();
        Assert.Equal(3, Track.PointCount); // already closed

        _vm.Undo();
        _vm.OutAndBack();
        Assert.Equal([A, B, A], Track.AllPoints.Select(p => p.Position));
    }

    [Fact]
    public void New_track_and_route_are_added_to_the_open_document_selected_and_in_drawing_mode()
    {
        _vm.NewGpx();

        _vm.NewTrack();
        Assert.Equal(["Track 1", "Track 2"], _vm.ActiveDocument!.Document.Data.Tracks.Select(t => t.Name));
        Assert.Same(Track, _vm.ActiveDocument.SelectedLayer!.Model);
        Assert.True(_vm.IsDrawing);

        _vm.NewRoute();
        var route = Assert.Single(_vm.ActiveDocument.Document.Data.Routes);
        Assert.Equal("Ruta 1", route.Name);
        Assert.Same(route, _vm.ActiveDocument.SelectedLayer!.Model);
    }

    [Fact]
    public void Mide_time_adds_half_of_the_smaller_of_horizontal_and_vertical_times()
    {
        // 4 km flat: 1 h. 2 km with +400 m: max(0,5 h, 1 h) + 0,5 h / 2 = 1,25 h. Descending 600 m in 1 km: 1 h + 0,25 h / 2.
        Assert.Equal(1, MideTime.LegHours(4000, 0, 0), 9);
        Assert.Equal(1.25, MideTime.LegHours(2000, 0, 400), 9);
        Assert.Equal(1.125, MideTime.LegHours(1000, 600, 0), 9);
        Assert.Equal(0.5, MideTime.LegHours(4000, null, 1000, scale: 2), 9);
    }

    [Fact]
    public void Regenerated_times_by_slope_use_the_mide_time_of_each_leg()
    {
        // 0,036° of latitude ≈ 4 km, flat: 1 h at 4 km/h; then the same distance back climbing 800 m: 2 h + 0,5 h.
        var start = new TrailPoint(new GeoPosition(43, -2)) { Elevation = 0 };
        var far = new TrailPoint(new GeoPosition(43.036, -2)) { Elevation = 0 };
        var back = new TrailPoint(new GeoPosition(43, -2)) { Elevation = 800 };
        var options = new TimeOptions(new DateTime(2026, 6, 1, 8, 0, 0), TimeZoneInfo.Utc, TimeMode.BySlope, 4, SpeedUnit.KilometersPerHour, TimeSpan.Zero);

        var plan = TimeRegenerator.Plan([[start, far, back]], options)!;

        var hours = plan.Times.Select(t => (t.Time - plan.StartUtc).TotalHours).ToList();
        Assert.Equal(0, hours[0]);
        Assert.InRange(hours[1], 0.99, 1.01);
        Assert.InRange(hours[2] - hours[1], 1.5, 2.6); // smoothing spreads the climb over the 100 m window
    }

    [Fact]
    public void A_brouter_answer_gives_the_points_with_their_elevation()
    {
        const string json = """
            {"type":"FeatureCollection","features":[{"type":"Feature","properties":{"track-length":"3810"},
             "geometry":{"type":"LineString","coordinates":[[-2.338100,42.951219,1104.5],[-2.338104,42.951282,1104.25]]}}]}
            """;

        var points = BRouterClient.Parse(json);

        Assert.Equal(new GeoPosition(42.951219, -2.3381), points[0].Position);
        Assert.Equal(1104.25, points[1].Elevation);
        Assert.Throws<RoutingException>(() => BRouterClient.Parse("target island detected for pos 0"));
    }

    private sealed class FakeRouter : IRouter
    {
        public IReadOnlyList<RoutedPoint> Way { get; set; } = [];

        public RoutingException? Error { get; set; }

        public string? LastProfile { get; private set; }

        public string Attribution => "Prueba";

        public Task<IReadOnlyList<RoutedPoint>> RouteAsync(GeoPosition from, GeoPosition to, string profile, CancellationToken cancellationToken)
        {
            LastProfile = profile;
            return Error is { } error ? Task.FromException<IReadOnlyList<RoutedPoint>>(error) : Task.FromResult(Way);
        }
    }

    private sealed class FakeTerrain : IElevationSource
    {
        public double? Value { get; set; }

        public bool Fail { get; set; }

        public string Name => "MDT prueba";

        public string VerticalDatum => "Ortométricas";

        public string Attribution => "Prueba";

        public Task<IReadOnlyList<double?>> GetElevationsAsync(IReadOnlyList<GeoPosition> positions, IProgress<double>? progress, CancellationToken cancellationToken) =>
            Fail
                ? Task.FromException<IReadOnlyList<double?>>(new ElevationSourceException("offline"))
                : Task.FromResult<IReadOnlyList<double?>>(positions.Select(_ => Value).ToList());
    }
}
