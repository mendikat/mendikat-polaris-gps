using Polaris.Gps.App.ViewModels;
using Polaris.Gps.Domain.Documents;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Gpx;
using Polaris.Gps.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Polaris.Gps.Tests.App;

/// <summary>Tools › Join tracks: tracks of every open document, preview, result in a new GPX document.</summary>
public sealed class JoinTracksFlowTests
{
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeDocumentStore _store = new();
    private readonly MainViewModel _vm;

    public JoinTracksFlowTests()
    {
        _vm = new MainViewModel(_dialogs, _store, new FakeSettingsStore(), NullLogger<MainViewModel>.Instance);
    }

    private static Track Line(string name, double fromLat, double toLat)
    {
        var segment = new TrackSegment();
        segment.Points.AddRange(Enumerable.Range(0, 3).Select(i => new TrailPoint(new GeoPosition(fromLat + (toLat - fromLat) * i / 2, -1.6))));
        var track = new Track { Name = name };
        track.Segments.Add(segment);
        return track;
    }

    private void Open(string path, string version, params Track[] tracks)
    {
        var data = new TrailData { FormatState = new GpxFormatState { SourceVersion = version } };
        data.Tracks.AddRange(tracks);
        _store.Contents[path] = data;
        _dialogs.OpenPaths.Enqueue(path);
        _vm.OpenAsync(DocumentFormat.Gpx).GetAwaiter().GetResult();
    }

    [Fact]
    public void Tracks_of_several_documents_are_joined_into_a_new_unsaved_gpx()
    {
        var up = Line("Subida", 42.800, 42.810);
        var down = Line("Bajada", 42.820, 42.8101); // drawn from the far end
        Open(@"C:\data\subida.gpx", "1.1", up);
        Open(@"C:\data\bajada.gpx", "1.1", down, new Track { Name = "Vacío" });
        JoinTracksViewModel? shown = null;
        _dialogs.ToolDialog = vm =>
        {
            shown = (JoinTracksViewModel)vm;
            return true;
        };

        Assert.True(_vm.JoinTracksCommand.CanExecute(null));
        _vm.JoinTracksCommand.Execute(null);

        Assert.Equal(["Subida — subida.gpx · 3 puntos", "Bajada — bajada.gpx · 3 puntos"], shown!.Items.Select(i => i.Label)); // no empty track
        Assert.Equal(["1. Subida", "2. Bajada (invertido) — hueco 11 m"], shown.Steps);
        Assert.Equal("2 tracks → 1 track de 6 puntos. Segmentos: 1. Huecos: 11 m en total.", shown.Preview);

        Assert.Equal(3, _vm.Documents.Count);
        var joined = _vm.ActiveDocument!;
        Assert.Equal("Sin título 1.gpx", joined.FileName);
        Assert.True(joined.IsDirty);
        Assert.False(joined.GpxUpgradeAccepted);
        var track = Assert.Single(joined.Document.Data.Tracks);
        Assert.Equal("Subida", track.Name);
        Assert.Equal(6, track.PointCount);
        Assert.Equal("2 tracks unidos en «Subida»: 6 puntos. Segmentos: 1.", _vm.StatusMessage);
        Assert.Equal(42.820, down.Segments[0].Points[0].Position.Latitude); // the originals do not change
        Assert.False(_vm.Documents[1].IsDirty);
    }

    [Fact]
    public void The_dialog_needs_two_tracks_and_a_valid_gap_and_cancelling_changes_nothing()
    {
        Open(@"C:\data\dos.gpx", "1.1", Line("A", 42.800, 42.810), Line("B", 42.815, 42.820));
        JoinTracksViewModel? shown = null;
        _dialogs.ToolDialog = vm =>
        {
            shown = (JoinTracksViewModel)vm;
            shown.MaxGapText = "abc";
            Assert.False(shown.CanApply);
            Assert.Equal("El hueco máximo debe ser un número de metros entre 0 y 100.000.", shown.Error);

            shown.MaxGapText = "100";
            Assert.Equal("2. B — hueco 555 m, segmento nuevo", shown.Steps[1]);

            shown.MaxGapText = "1000";
            Assert.Equal("2. B — hueco 555 m", shown.Steps[1]);

            shown.Items[1].IsSelected = false;
            Assert.False(shown.CanApply);
            Assert.Equal("Marca al menos dos tracks.", shown.Error);
            return false;
        };

        _vm.JoinTracksCommand.Execute(null);

        Assert.NotNull(shown);
        Assert.Single(_vm.Documents);
    }

    [Fact]
    public void Join_is_disabled_with_fewer_than_two_tracks_with_points()
    {
        Assert.False(_vm.JoinTracksCommand.CanExecute(null));
        Open(@"C:\data\uno.gpx", "1.1", Line("A", 42.800, 42.810));

        Assert.False(_vm.JoinTracksCommand.CanExecute(null));
        _vm.JoinTracks();
        Assert.Equal("Para unir hacen falta al menos dos tracks con puntos en los documentos abiertos.", _vm.StatusMessage);

        Open(@"C:\data\otro.gpx", "1.1", Line("B", 42.815, 42.820));
        Assert.True(_vm.JoinTracksCommand.CanExecute(null));
    }

    [Fact]
    public void Tracks_from_a_gpx_10_file_are_saved_as_11_without_asking()
    {
        Open(@"C:\data\viejo.gpx", "1.0", Line("A", 42.800, 42.810));
        Open(@"C:\data\nuevo.gpx", "1.1", Line("B", 42.8101, 42.820));
        _dialogs.ToolDialog = _ => true;

        _vm.JoinTracks();

        Assert.Equal("1.0", ((GpxFormatState)_vm.ActiveDocument!.Document.Data.FormatState!).SourceVersion);
        Assert.True(_vm.ActiveDocument.GpxUpgradeAccepted);
    }
}
