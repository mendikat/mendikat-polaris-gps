using Mendikat.TrailLab.App.ViewModels;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Elevation;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Domain.Model;
using Mendikat.TrailLab.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mendikat.TrailLab.Tests.App;

/// <summary>Datum and elevation under the pointer in the status bar (COORD-07, COORD-08).</summary>
public sealed class PointerStatusTests
{
    private static readonly GeoPosition Here = new(42.8, -1.6);

    private readonly FakeDialogService _dialogs = new();
    private readonly TerrainSource _terrain = new();
    private readonly MainViewModel _vm;

    public PointerStatusTests()
    {
        _vm = new MainViewModel(_dialogs, new FakeDocumentStore(), new FakeSettingsStore(), NullLogger<MainViewModel>.Instance)
        {
            ElevationSources = _ => _terrain,
            ElevationDelay = TimeSpan.Zero,
        };
    }

    private async Task MoveTo(GeoPosition? position)
    {
        _vm.UpdatePointer(position);
        await _vm.PointerElevationLookup;
    }

    [Fact]
    [Trait("Req", "COORD-07")]
    public async Task The_datum_of_the_file_is_shown_and_is_wgs84_without_files()
    {
        Assert.Equal("WGS84", _vm.PointerDatum);

        _dialogs.OpenPaths.Enqueue(@"C:\data\a.gpx");
        await _vm.OpenAsync(DocumentFormat.Gpx);

        Assert.Equal("WGS84", _vm.PointerDatum);
        Assert.Equal(TrailData.Wgs84, _vm.ActiveDocument!.Document.Data.Datum);
    }

    [Fact]
    [Trait("Req", "COORD-08")]
    public async Task Over_a_point_of_the_file_its_elevation_is_shown_without_querying_the_terrain()
    {
        _vm.PointAt = _ => new TrailPoint(Here) { Elevation = 1234.4 };

        await MoveTo(Here);

        Assert.Equal("Alt. 1.234 m (archivo)", _vm.PointerElevation);
        Assert.Equal(0, _terrain.Queries);
    }

    [Fact]
    [Trait("Req", "COORD-08")]
    public async Task Elsewhere_the_terrain_elevation_of_the_dem_is_shown()
    {
        _vm.PointAt = _ => new TrailPoint(Here); // a point without elevation does not count
        _terrain.Value = 845.6;

        await MoveTo(Here);

        Assert.Equal("Alt. 846 m (MDT prueba)", _vm.PointerElevation);
        Assert.Contains("Ortométricas", _vm.PointerElevationToolTip, StringComparison.Ordinal);
        Assert.Equal(1, _terrain.Queries);
    }

    [Fact]
    [Trait("Req", "COORD-08")]
    public async Task Without_dem_data_or_outside_the_map_nothing_is_shown()
    {
        _terrain.Value = null;
        await MoveTo(Here);
        Assert.Equal("—", _vm.PointerElevation);

        _terrain.Value = 500;
        await MoveTo(Here);
        await MoveTo(null);
        Assert.Equal("—", _vm.PointerElevation);
        Assert.Null(_vm.PointerElevationToolTip);
    }

    [Fact]
    [Trait("Req", "COORD-08")]
    public async Task Moving_the_pointer_cancels_the_pending_lookup()
    {
        var vm = new MainViewModel(_dialogs, new FakeDocumentStore(), new FakeSettingsStore(), NullLogger<MainViewModel>.Instance)
        {
            ElevationSources = _ => _terrain,
            ElevationDelay = TimeSpan.FromSeconds(30),
        };

        vm.UpdatePointer(Here);
        var first = vm.PointerElevationLookup;
        vm.UpdatePointer(null);
        await first;

        Assert.Equal(0, _terrain.Queries);
        Assert.Equal("—", vm.PointerElevation);
    }

    [Fact]
    [Trait("Req", "COORD-08")]
    [Trait("Req", "NFR-05")]
    public async Task Without_connection_the_terrain_is_not_queried_again_for_a_while()
    {
        _terrain.Fail = true;
        await MoveTo(Here);
        await MoveTo(new GeoPosition(42.9, -1.6));

        Assert.Equal("—", _vm.PointerElevation);
        Assert.Equal(1, _terrain.Queries);
    }

    private sealed class TerrainSource : IElevationSource
    {
        public double? Value { get; set; } = 1000;

        public bool Fail { get; set; }

        public int Queries { get; private set; }

        public string Name => "MDT prueba";

        public string VerticalDatum => "Ortométricas";

        public string Attribution => "Prueba";

        public Task<IReadOnlyList<double?>> GetElevationsAsync(
            IReadOnlyList<GeoPosition> positions, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            Queries++;
            if (Fail)
            {
                throw new ElevationSourceException("offline");
            }

            return Task.FromResult<IReadOnlyList<double?>>(positions.Select(_ => Value).ToList());
        }
    }
}
