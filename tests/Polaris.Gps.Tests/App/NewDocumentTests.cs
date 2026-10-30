using Polaris.Gps.App.Services;
using Polaris.Gps.App.ViewModels;
using Polaris.Gps.Domain.Documents;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Gpx;
using Polaris.Gps.Tests.Fakes;
using Polaris.Gps.Tests.Gpx;
using Microsoft.Extensions.Logging.Abstractions;

namespace Polaris.Gps.Tests.App;

/// <summary>File › New GPX and drawing points with Ctrl+click (APP-08).</summary>
public sealed class NewDocumentTests
{
    private static readonly GeoPosition A = new(43.0, -2.0);
    private static readonly GeoPosition B = new(43.001, -2.001);
    private static readonly GeoPosition C = new(43.002, -2.002);

    private readonly FakeDialogService _dialogs = new();
    private readonly FakeDocumentStore _store = new() { SavingSupported = true };
    private readonly MainViewModel _vm;

    public NewDocumentTests()
    {
        _vm = new MainViewModel(_dialogs, _store, new FakeSettingsStore(), NullLogger<MainViewModel>.Instance);
    }

    private TrailData Data => _vm.ActiveDocument!.Document.Data;

    [Fact]
    public void New_gpx_opens_an_untitled_document_with_an_empty_selected_track()
    {
        _vm.NewGpx();

        var doc = _vm.ActiveDocument!;
        Assert.Equal("Sin título 1.gpx", doc.FileName);
        Assert.Null(doc.FilePath);
        Assert.True(doc.Document.IsNew);
        Assert.False(doc.IsDirty);
        Assert.Equal(DocumentFormat.Gpx, doc.Document.Format);
        Assert.Equal("GPX 1.1", doc.FormatLabel);
        var track = Assert.Single(Data.Tracks);
        Assert.Equal("Track 1", track.Name);
        Assert.Equal(0, track.PointCount);
        Assert.Same(track, doc.SelectedLayer!.Model);
    }

    [Fact]
    public void Each_new_document_gets_its_own_number_and_tab()
    {
        _vm.NewGpx();
        _vm.NewGpx();

        Assert.Equal(["Sin título 1.gpx", "Sin título 2.gpx"], _vm.Documents.Select(d => d.FileName));
        Assert.Equal("Sin título 2.gpx", _vm.ActiveDocument!.FileName);
    }

    [Fact]
    public void Ctrl_click_appends_points_to_the_track_selecting_each_new_one()
    {
        _vm.NewGpx();

        _vm.AddPointAt(A);
        _vm.AddPointAt(B);

        var points = Data.Tracks[0].Segments[0].Points;
        Assert.Equal([A, B], points.Select(p => p.Position));
        Assert.All(points, p => Assert.True(p.IsAdded));
        Assert.All(points, p => Assert.Null(p.Elevation));
        Assert.Same(points[1], _vm.ActiveDocument!.SelectedRows.Single().Point);
        Assert.True(_vm.ActiveDocument.IsDirty);
        Assert.Equal("Sin título 1.gpx *", _vm.ActiveDocument.Header);
    }

    [Fact]
    public void Ctrl_click_with_a_point_selected_inserts_right_after_it()
    {
        _vm.NewGpx();
        _vm.AddPointAt(A);
        _vm.AddPointAt(C);
        var doc = _vm.ActiveDocument!;
        doc.SelectPoints([Data.Tracks[0].Segments[0].Points[0]]);

        _vm.AddPointAt(B);

        Assert.Equal([A, B, C], Data.Tracks[0].Segments[0].Points.Select(p => p.Position));
    }

    [Fact]
    public void Adding_a_point_is_undoable_back_to_the_clean_state()
    {
        _vm.NewGpx();
        _vm.AddPointAt(A);

        _vm.Undo();

        Assert.Equal(0, Data.Tracks[0].PointCount);
        Assert.False(_vm.ActiveDocument!.IsDirty);
    }

    [Fact]
    public void Ctrl_click_with_no_layer_creates_a_track()
    {
        _vm.NewGpx();
        Data.Tracks.Clear();
        _vm.ActiveDocument!.Document.MarkChanged();
        Assert.Null(_vm.ActiveDocument.SelectedLayer);

        _vm.AddPointAt(A);

        Assert.Equal(A, Assert.Single(Assert.Single(Data.Tracks).AllPoints).Position);
        Assert.Same(Data.Tracks[0], _vm.ActiveDocument.SelectedLayer!.Model);
    }

    [Fact]
    public async Task Saving_a_new_document_asks_for_a_path_and_then_uses_it()
    {
        _vm.NewGpx();
        _vm.AddPointAt(A);
        var path = Path.Combine(Path.GetTempPath(), "polarisgps-nuevo");
        _dialogs.SavePath = path;

        Assert.True(await _vm.SaveActiveAsync());

        var doc = _vm.ActiveDocument!;
        Assert.Equal([path + ".gpx"], _store.SavedPaths);
        Assert.Equal("polarisgps-nuevo.gpx", doc.FileName);
        Assert.False(doc.Document.IsNew);
        Assert.False(doc.IsDirty);

        _vm.AddPointAt(B);
        _dialogs.SavePath = null;
        Assert.True(await _vm.SaveActiveAsync());
        Assert.Equal(path + ".gpx", _store.SavedPaths[^1]);
    }

    [Fact]
    public async Task Cancelling_the_path_when_closing_keeps_the_new_document_open()
    {
        _vm.NewGpx();
        _vm.AddPointAt(A);
        _dialogs.SaveChangesAnswer = SaveChangesChoice.Save;
        _dialogs.SavePath = null;

        Assert.False(await _vm.CloseDocumentAsync(_vm.ActiveDocument));

        Assert.Single(_vm.Documents);
        Assert.Empty(_store.SavedPaths);
        Assert.Equal(["Sin título 1.gpx"], _dialogs.SaveChangesQuestions);
    }

    [Fact]
    public async Task An_untouched_new_document_closes_without_asking()
    {
        _vm.NewGpx();

        Assert.True(await _vm.CloseDocumentAsync(_vm.ActiveDocument));

        Assert.Empty(_dialogs.SaveChangesQuestions);
        Assert.Empty(_vm.Documents);
    }

    [Fact]
    public void A_drawn_document_is_written_as_valid_gpx_1_1()
    {
        _vm.NewGpx();
        _vm.AddPointAt(A);
        _vm.AddPointAt(B);

        var bytes = TrailFileWriter.Serialize(Data, DocumentFormat.Gpx);

        Assert.Empty(GpxWriterTests.SchemaErrors(bytes));
        Assert.True(TrailFileWriter.Verify(bytes, DocumentFormat.Gpx, TrailFileWriter.Shape(Data)));
        Assert.Empty(TrailFileWriter.Check(Data, DocumentFormat.Gpx));
        using var stream = new MemoryStream(bytes);
        var reread = new GpxReader().Read(stream, TestContext.Current.CancellationToken).Data;
        Assert.Equal("Track 1", reread.Tracks.Single().Name);
        Assert.Equal([A, B], reread.Tracks[0].AllPoints.Select(p => p.Position));
    }
}
