using Mendikat.TrailLab.App.ViewModels;
using Mendikat.TrailLab.Mapping.Local;
using Mendikat.TrailLab.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mendikat.TrailLab.Tests.App;

/// <summary>
/// ECW and ECWP are opened only after the user accepts the ERDAS ECW/JP2 end-user licence, as its SDK licence requires;
/// ECWP streams can be opened from the File menu; "About" shows the Intergraph notice.
/// </summary>
public sealed class EcwLicenseTests
{
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeSettingsStore _settings = new();
    private readonly List<string> _loaded = [];
    private readonly MainViewModel _vm;

    public EcwLicenseTests()
    {
        _vm = new MainViewModel(_dialogs, new FakeDocumentStore(), _settings, NullLogger<MainViewModel>.Instance)
        {
            // Records what would be read and fails like an unreadable file, so nothing real is opened.
            LocalMapLoader = (path, _) =>
            {
                _loaded.Add(path);
                throw new LocalMapException(LocalMapError.ReadFailed, GdalRaster.DisplayName(path));
            },
        };
    }

    [Fact]
    public async Task Declining_the_licence_opens_no_ecw_and_asks_again_next_time()
    {
        _dialogs.EcwLicenseAnswer = false;

        await _vm.OpenLocalMapAsync(@"C:\mapas\hoja.ecw");
        await _vm.OpenLocalMapAsync(@"C:\mapas\otra.ECW");

        Assert.Empty(_loaded);
        Assert.Equal(2, _dialogs.EcwLicenseShown);
        Assert.False(_settings.Current.EcwLicenseAccepted);
        Assert.Equal("ECW desactivado: no se ha aceptado su licencia de uso.", _vm.StatusMessage);
    }

    [Fact]
    public async Task Accepting_the_licence_is_remembered()
    {
        _dialogs.EcwLicenseAnswer = true;

        await _vm.OpenLocalMapAsync(@"C:\mapas\hoja.ecw");
        await _vm.OpenLocalMapAsync(@"C:\mapas\otra.ecw");

        Assert.Equal([@"C:\mapas\hoja.ecw", @"C:\mapas\otra.ecw"], _loaded);
        Assert.Equal(1, _dialogs.EcwLicenseShown);
        Assert.True(_settings.Current.EcwLicenseAccepted);
    }

    [Fact]
    public async Task Other_formats_never_ask_for_the_licence()
    {
        await _vm.OpenLocalMapAsync(@"C:\mapas\orto.tif");

        Assert.Equal([@"C:\mapas\orto.tif"], _loaded);
        Assert.Equal(0, _dialogs.EcwLicenseShown);
    }

    [Fact]
    public async Task An_ecwp_stream_is_opened_from_its_address_after_the_licence()
    {
        _settings.Current = _settings.Current with { EcwLicenseAccepted = true };
        _dialogs.EcwpUrl = "  ecwp://servidor.ejemplo.org/imagenes/orto.ecw ";

        _vm.OpenEcwpCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(["ecwp://servidor.ejemplo.org/imagenes/orto.ecw"], _loaded);
        Assert.Contains("orto.ecw", Assert.Single(_dialogs.Errors), StringComparison.Ordinal); // the fake loader's error, named by its address
    }

    [Fact]
    public void Ecwp_addresses_are_recognised()
    {
        Assert.True(LocalMapLoader.IsEcwpUrl("ecwp://a/b.ecw"));
        Assert.True(LocalMapLoader.IsEcwpUrl("ECWPS://a/b.ecw"));
        Assert.False(LocalMapLoader.IsEcwpUrl(@"C:\ecwp\b.ecw"));
        Assert.True(GdalRaster.IsEcw("ecwp://a/b"));
        Assert.Equal("b.ecw", GdalRaster.DisplayName("ecwp://a/imagenes/b.ecw"));
    }

    [Fact]
    public void About_shows_the_intergraph_notice()
    {
        Assert.StartsWith("This application supports the ECW data format and ECWP compression protocols.", Mendikat.TrailLab.App.Resources.Strings.AboutEcwNotice, StringComparison.Ordinal);
        Assert.Contains("Copyright 2007-2013 Intergraph Corporation", Mendikat.TrailLab.App.Resources.Strings.AboutEcwNotice, StringComparison.Ordinal);
    }
}
