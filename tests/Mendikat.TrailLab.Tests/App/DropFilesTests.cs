using Mendikat.TrailLab.App.Map;
using Mendikat.TrailLab.App.ViewModels;
using Mendikat.TrailLab.Mapping.Local;
using Mendikat.TrailLab.Tests.Fakes;
using Mendikat.TrailLab.Tests.Mapping;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mendikat.TrailLab.Tests.App;

/// <summary>Files dropped on the window: tracks as documents, maps and DEMs as user cartography (APP-10).</summary>
public sealed class DropFilesTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "traillab-drop-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly FakeDialogService _dialogs = new();
    private readonly MainViewModel _vm;

    public DropFilesTests()
    {
        Directory.CreateDirectory(_folder);
        _vm = new MainViewModel(_dialogs, new FakeDocumentStore(), new FakeSettingsStore(), NullLogger<MainViewModel>.Instance)
        {
            LocalMapLoader = (path, token) => LocalMapLoader.Load(path, new WpfRasterImageDecoder(), token),
        };
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string Dem()
    {
        var path = Path.Combine(_folder, "mdt.asc");
        File.WriteAllText(path, "ncols 2\nnrows 2\nxllcenter 600000\nyllcenter 4750000\ncellsize 50\n1 2\n3 4\n");
        File.WriteAllText(Path.ChangeExtension(path, ".prj"), LocalDemTests.EtrsUtm30Prj);
        return path;
    }

    [Fact]
    public async Task Dropped_tracks_and_cartography_each_go_to_their_place()
    {
        await _vm.OpenFilesAsync([@"C:\data\ruta.gpx", Dem(), @"C:\data\vuelta.KML"]);

        Assert.Equal(["ruta.gpx", "vuelta.KML"], _vm.Documents.Select(d => d.FileName)); // the fake store opens any path
        Assert.Equal(["mdt.asc"], _vm.LocalMaps.Select(m => m.Name));
    }

    [Fact]
    public async Task Other_files_and_folders_are_skipped_and_counted()
    {
        await _vm.OpenFilesAsync([Path.Combine(_folder, "notas.docx"), _folder, @"C:\data\ruta.gpx"]);

        Assert.Single(_vm.Documents);
        Assert.Equal("2 archivos soltados no se han abierto: no son tracks (GPX, KML, KMZ, TCX, OziExplorer, CompeGPS/TwoNav) ni cartografía admitida.", _vm.StatusMessage);
        Assert.Empty(_dialogs.Errors);
    }

    [Theory]
    [InlineData(new[] { @"C:\a.gpx" }, true)]
    [InlineData(new[] { @"C:\a.txt", @"C:\b.HGT" }, true)]
    [InlineData(new[] { @"C:\a.txt", @"C:\b.docx" }, false)]
    public void The_drop_cursor_accepts_only_supported_files(string[] paths, bool expected)
    {
        Assert.Equal(expected, MainViewModel.CanOpenAny(paths));
    }
}
