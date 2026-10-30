using System.Net;
using Polaris.Gps.App.Map;
using Polaris.Gps.App.ViewModels;
using Polaris.Gps.Mapping.Local;
using Polaris.Gps.Mapping.Providers;
using Polaris.Gps.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Polaris.Gps.Tests.App;

/// <summary>
/// User cartographies: drawing order by dragging (first in the list on top), opacity, and the online cartography always
/// at the bottom (LMAP-11).
/// </summary>
public sealed class UserCartographyOrderTests
{
    private readonly MainViewModel _vm = new(new FakeDialogService(), new FakeDocumentStore(), new FakeSettingsStore(), NullLogger<MainViewModel>.Instance);

    /// <summary>A 10 × 10 image calibrated over 1° × 1°, without reading any file.</summary>
    private static LocalMapItemViewModel Raster(string name)
    {
        var calibration = new MapCalibration("Prueba", name, name, Crs.Geographic(MapDatum.Wgs84), [new(0, 0, -2, 43), new(10, 10, -1, 42)]);
        var map = GeoreferencedMap.Create(calibration, 10, 10);
        return new LocalMapItemViewModel(new LocalRasterMap(@"C:\mapas\" + name, map, new RasterImage(1, 1, 0.1, new byte[4])));
    }

    [Fact]
    public void Moving_reorders_the_list_and_announces_it()
    {
        _vm.LocalMaps.Add(Raster("a.map"));
        _vm.LocalMaps.Add(Raster("b.map"));
        _vm.LocalMaps.Add(Raster("c.map"));
        var reordered = 0;
        _vm.LocalMapsReordered += (_, _) => reordered++;

        _vm.MoveLocalMap(2, 0);
        _vm.MoveLocalMap(0, 0); // nothing to do
        _vm.MoveLocalMap(5, 0); // out of range: ignored

        Assert.Equal(["c.map", "a.map", "b.map"], _vm.LocalMaps.Select(m => m.Name));
        Assert.Equal(1, reordered);
    }

    [Fact]
    public void Opacity_is_clamped_shown_as_percent_and_announced()
    {
        var item = Raster("a.map");
        var changes = 0;
        item.OpacityChanged += (_, _) => changes++;

        item.Opacity = 0.75;
        Assert.Equal("75 %", item.OpacityText);
        item.Opacity = 2;

        Assert.Equal(1, item.Opacity);
        Assert.Equal(2, changes);
        Assert.Equal("Opacidad de a.map", item.OpacityName);
        Assert.True(item.HasOpacity);
    }

    [Fact]
    public void On_the_map_user_cartographies_stay_above_the_online_one_in_list_order()
    {
        WindowSmokeTests.OnStaThread(() =>
        {
            var map = new Mapsui.Map();
            using var http = new HttpClient(new Offline());
            using var controller = new MapController(map, http, NullLogger<MapController>.Instance);
            controller.SetProvider(MapProviderCatalog.Default);
            var (a, b) = (Raster("a.map"), Raster("b.map"));
            controller.AddLocalMap(a);
            controller.AddLocalMap(b); // the newest is drawn on top

            string[] Order() => map.Layers.Select(l => l.Name).Where(n => n.StartsWith("local:", StringComparison.Ordinal) || n.StartsWith("base:", StringComparison.Ordinal)).ToArray();

            Assert.Equal(["base:ign-es", "local:a.map", "local:b.map"], Order()); // drawn bottom to top
            controller.OrderLocalMaps([a, b]); // a first in the list: on top
            Assert.Equal(["base:ign-es", "local:b.map", "local:a.map"], Order());

            a.Opacity = 0.4;
            controller.SetLocalMapOpacity(a);
            Assert.Equal(0.4, map.Layers.Single(l => l.Name == "local:a.map").Opacity);

            controller.SetProvider(MapProviderCatalog.Find("opentopomap")!); // changing the online cartography keeps it below
            Assert.Equal(["base:opentopomap", "local:b.map", "local:a.map"], Order());
        });
    }

    private sealed class Offline : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }
}
