using Polaris.Gps.App.ViewModels;
using Polaris.Gps.Domain.Documents;
using Polaris.Gps.Domain.Elevation;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Mapping.Elevation;
using Polaris.Gps.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Polaris.Gps.Tests.App;

/// <summary>DEM adjustment through the view models with a fake elevation source (increment 7).</summary>
public sealed class DemFlowTests
{
    private const string Path = @"C:\data\dem.gpx";

    private readonly FakeDialogService _dialogs = new();
    private readonly FakeDocumentStore _store = new();
    private readonly FakeElevationSource _source = new();
    private readonly MainViewModel _vm;
    private readonly TrailData _data = new();

    public DemFlowTests()
    {
        var segment = new TrackSegment();
        segment.Points.AddRange(Enumerable.Range(0, 4).Select(i => new TrailPoint(new GeoPosition(42 + i * 0.001, -1.6)) { Elevation = 100 + i }));
        var track = new Track { Name = "Valle" };
        track.Segments.Add(segment);
        _data.Tracks.Add(track);
        _store.Contents[Path] = _data;
        _vm = new MainViewModel(_dialogs, _store, new FakeSettingsStore(), NullLogger<MainViewModel>.Instance)
        {
            ElevationSources = resolution =>
            {
                _source.LastResolution = resolution;
                return _source;
            },
        };
        _dialogs.OpenPaths.Enqueue(Path);
        _vm.OpenAsync(DocumentFormat.Gpx).GetAwaiter().GetResult();
    }

    private List<TrailPoint> Points => _data.Tracks[0].Segments[0].Points;

    /// <summary>The user presses "Consultar", reads the preview and presses "Aplicar" (or not).</summary>
    private void UserQueriesAnd(bool apply, Action<DemViewModel>? inspect = null) =>
        _dialogs.ToolDialog = vm =>
        {
            var dem = (DemViewModel)vm;
            dem.QueryAsync().GetAwaiter().GetResult();
            inspect?.Invoke(dem);
            return apply && dem.CanApply;
        };

    [Fact]
    public void Preview_shows_counts_and_applying_changes_only_points_with_data()
    {
        _source.Values = [200, null, 102, 300];
        string? preview = null;
        UserQueriesAnd(apply: true, dem => preview = dem.Preview);

        _vm.AdjustElevationsCommand.Execute(null);

        Assert.StartsWith("4 puntos: cambiarán 2 · iguales 1 · sin dato del MDT 1 (conservan su elevación).", preview);
        Assert.Contains("Diferencia MDT − original: mínima +100,0 m · máxima +197,0 m · media +148,5 m.", preview);
        Assert.Equal([200, 101, 102, 300], Points.Select(p => p.Elevation));
        Assert.Equal(ElevationSource.Dem, Points[0].ElevationSource);
        Assert.Equal("Altitudes ajustadas con el MDT en «Valle»: 2 puntos.", _vm.StatusMessage);

        _vm.Undo();
        Assert.Equal([100, 101, 102, 103], Points.Select(p => p.Elevation));
    }

    [Fact]
    public void Without_applying_nothing_changes()
    {
        _source.Values = [200, 201, 202, 203];
        UserQueriesAnd(apply: false);

        _vm.AdjustElevationsCommand.Execute(null);

        Assert.Equal([100, 101, 102, 103], Points.Select(p => p.Elevation));
        Assert.False(_vm.ActiveDocument!.IsDirty);
    }

    [Fact]
    public void A_service_failure_is_shown_in_the_dialog_and_changes_nothing()
    {
        _source.Failure = new ElevationSourceException("offline");
        DemViewModel? shown = null;
        UserQueriesAnd(apply: true, dem => shown = dem);

        _vm.AdjustElevationsCommand.Execute(null);

        Assert.False(shown!.CanApply);
        Assert.StartsWith("No se pudo consultar el servicio de elevaciones del IGN", shown.Error);
        Assert.False(_vm.ActiveDocument!.IsDirty);
    }

    [Fact]
    public void Cancelling_the_download_changes_nothing()
    {
        _source.Failure = new OperationCanceledException();
        DemViewModel? shown = null;
        UserQueriesAnd(apply: true, dem => shown = dem);

        _vm.AdjustElevationsCommand.Execute(null);

        Assert.Equal("Consulta detenida. No se ha cambiado nada.", shown!.Error);
        Assert.False(_vm.ActiveDocument!.IsDirty);
    }

    [Fact]
    public async Task The_dialog_shows_the_vertical_datum_and_the_chosen_resolution_is_used()
    {
        _source.Values = [100, 101, 102, 103];
        UserQueriesAnd(apply: false, dem =>
        {
            Assert.Contains("Referencia vertical: ortométrica de prueba", dem.SourceInfo);
            Assert.Contains("No hay nada que cambiar.", dem.Preview);
        });
        _vm.AdjustElevationsCommand.Execute(null);

        var dialog = new DemViewModel(_vm.ActiveDocument!.SelectedLayer!, _vm.ElevationSources!)
        {
            Resolution = new DemResolutionOption(IgnDemResolution.TwentyFiveMeters, "25 m"),
        };
        await dialog.QueryAsync();
        Assert.Equal(IgnDemResolution.TwentyFiveMeters, _source.LastResolution);
    }

    [Fact]
    public void Without_an_elevation_source_or_for_waypoints_the_tool_is_disabled()
    {
        Assert.True(_vm.AdjustElevationsCommand.CanExecute(null));

        var plain = new MainViewModel(_dialogs, _store, new FakeSettingsStore(), NullLogger<MainViewModel>.Instance);
        Assert.False(plain.AdjustElevationsCommand.CanExecute(null));
    }

    private sealed class FakeElevationSource : IElevationSource
    {
        public IReadOnlyList<double?> Values { get; set; } = [];

        public Exception? Failure { get; set; }

        public IgnDemResolution? LastResolution { get; set; }

        public string Name => "Fake";

        public string VerticalDatum => "ortométrica de prueba";

        public string Attribution => "© prueba";

        public Task<IReadOnlyList<double?>> GetElevationsAsync(IReadOnlyList<GeoPosition> positions, IProgress<double>? progress, CancellationToken cancellationToken) =>
            Failure is not null ? Task.FromException<IReadOnlyList<double?>>(Failure) : Task.FromResult(Values);
    }
}
