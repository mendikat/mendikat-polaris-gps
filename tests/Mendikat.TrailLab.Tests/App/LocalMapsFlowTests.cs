using Mendikat.TrailLab.App.Map;
using Mendikat.TrailLab.App.ViewModels;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Mapping.Local;
using Mendikat.TrailLab.Tests.Fakes;
using Mendikat.TrailLab.Tests.Mapping;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mendikat.TrailLab.Tests.App;

/// <summary>File › Open cartography, the "Local maps" list and the use of local DEMs (LMAP-01, LMAP-02, LDEM-01).</summary>
public sealed class LocalMapsFlowTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "traillab-localflow-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly FakeDialogService _dialogs = new();
    private readonly MainViewModel _vm;
    private readonly List<string> _events = [];

    public LocalMapsFlowTests()
    {
        Directory.CreateDirectory(_folder);
        _vm = new MainViewModel(_dialogs, new FakeDocumentStore(), new FakeSettingsStore(), NullLogger<MainViewModel>.Instance)
        {
            LocalMapLoader = (path, token) => LocalMapLoader.Load(path, new WpfRasterImageDecoder(), token),
            ElevationDelay = TimeSpan.Zero,
        };
        _vm.LocalMapAdded += (_, item) => _events.Add("+" + item.Name);
        _vm.LocalMapRemoved += (_, item) => _events.Add("-" + item.Name);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>3 × 3 grid of 50 m in ETRS89 UTM 30N whose north-west node is (600000, 4750100).</summary>
    private string Dem(string name = "mdt.asc")
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, "ncols 3\nnrows 3\nxllcenter 600000\nyllcenter 4750000\ncellsize 50\n100 110 120\n200 210 220\n300 310 320\n");
        File.WriteAllText(Path.ChangeExtension(path, ".prj"), LocalDemTests.EtrsUtm30Prj);
        return path;
    }

    /// <summary>A .map whose image is missing.</summary>
    private string MapWithoutImage()
    {
        var path = Path.Combine(_folder, "roto.map");
        File.WriteAllText(path, "OziExplorer Map Data File Version 2.2\r\nRoto\r\nno-esta.jpg\r\n1 ,Map Code,\r\nWGS 84,WGS 84,0,0,WGS 84\r\n");
        return path;
    }

    private static readonly GeoPosition InsideDem = Crs.Utm(30, true, MapDatum.Etrs89).ToWgs84(600025, 4750075); // 155 m

    [Fact]
    public async Task A_dem_is_listed_and_used_for_the_pointer_elevation()
    {
        _dialogs.LocalMapPaths.Enqueue([Dem()]);

        await _vm.OpenLocalMapsAsync();

        var item = Assert.Single(_vm.LocalMaps);
        Assert.True(item.IsDem);
        Assert.Equal("Cartografías Usuario (1)", _vm.LocalMapsHeader);
        Assert.Equal(["+mdt.asc"], _events);
        Assert.StartsWith("MDT «mdt.asc» abierto", _vm.StatusMessage, StringComparison.Ordinal);
        Assert.Contains("UTM 30N ETRS89", item.Details, StringComparison.Ordinal);

        _vm.UpdatePointer(InsideDem);
        await _vm.PointerElevationLookup;
        Assert.Equal("Alt. 155 m (mdt.asc)", _vm.PointerElevation);
    }

    [Fact]
    public async Task An_unchecked_dem_is_not_used()
    {
        await _vm.OpenLocalMapAsync(Dem());

        _vm.LocalMaps[0].IsVisible = false;
        _vm.UpdatePointer(InsideDem);
        await _vm.PointerElevationLookup;

        Assert.Empty(_vm.LocalDems);
        Assert.Equal("—", _vm.PointerElevation);
    }

    [Fact]
    public async Task Adjust_elevations_offers_the_local_dems_after_the_ign_ones()
    {
        await _vm.OpenLocalMapAsync(Dem());
        var layer = new LayerItemViewModel(LayerKind.Track, new Mendikat.TrailLab.Domain.Model.Track());

        var dialog = new DemViewModel(layer, _ => throw new InvalidOperationException("IGN not used"), _vm.LocalDems);

        Assert.Equal(["mdt.asc"], dialog.Resolutions.Skip(2).Select(r => r.DisplayName));
        dialog.Resolution = dialog.Resolutions[2];
        Assert.Contains("desconocida", dialog.SourceInfo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failure_explains_why_and_adds_nothing()
    {
        await _vm.OpenLocalMapAsync(MapWithoutImage());

        Assert.Empty(_vm.LocalMaps);
        Assert.Empty(_events);
        var message = Assert.Single(_dialogs.Errors);
        Assert.Contains("roto.map", message, StringComparison.Ordinal);
        Assert.Contains("no-esta.jpg", message, StringComparison.Ordinal);
        Assert.False(_vm.IsBusy);
    }

    [Fact]
    public async Task A_map_can_be_removed_and_is_not_opened_twice()
    {
        var path = Dem();
        await _vm.OpenLocalMapAsync(path);
        await _vm.OpenLocalMapAsync(path);
        Assert.Single(_vm.LocalMaps);
        Assert.Equal("«mdt.asc» ya está abierto.", _vm.StatusMessage);

        _vm.RemoveLocalMapCommand.Execute(_vm.LocalMaps[0]);

        Assert.Empty(_vm.LocalMaps);
        Assert.Equal(["+mdt.asc", "-mdt.asc"], _events);
        Assert.Equal("Cartografías Usuario (0)", _vm.LocalMapsHeader);
    }

    [Fact]
    public async Task Maps_and_dems_chosen_with_open_file_are_opened_as_cartography()
    {
        _dialogs.OpenPaths.Enqueue(Dem());
        _dialogs.OpenPaths.Enqueue(MapWithoutImage());

        await _vm.OpenAsync(DocumentFormat.Gpx);
        await _vm.OpenAsync(DocumentFormat.Gpx);

        Assert.Empty(_vm.Documents);
        Assert.Equal(["mdt.asc"], _vm.LocalMaps.Select(m => m.Name));
        Assert.Contains("no-esta.jpg", Assert.Single(_dialogs.Errors), StringComparison.Ordinal); // the map's own error, not «XML no válido»
    }

    [Fact]
    public async Task Opening_cartography_does_not_touch_the_documents()
    {
        _dialogs.OpenPaths.Enqueue(@"C:\data\a.gpx");
        await _vm.OpenAsync(DocumentFormat.Gpx);

        await _vm.OpenLocalMapAsync(Dem());

        Assert.Single(_vm.Documents);
        Assert.False(_vm.Documents[0].IsDirty);
    }
}
