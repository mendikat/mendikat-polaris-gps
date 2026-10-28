using Mendikat.TrailLab.App.Services;
using Mendikat.TrailLab.App.ViewModels;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mendikat.TrailLab.Tests.App;

public sealed class MainViewModelTests
{
    private const string PathA = @"C:\data\a.gpx";
    private const string PathB = @"C:\data\b.gpx";

    private readonly FakeDialogService _dialogs = new();
    private readonly FakeDocumentStore _store = new();
    private readonly FakeSettingsStore _settings = new();
    private readonly MainViewModel _vm;

    public MainViewModelTests()
    {
        _vm = CreateViewModel();
    }

    [Fact]
    public void Without_documents_only_open_and_exit_are_enabled()
    {
        Assert.True(_vm.OpenGpxCommand.CanExecute(null));
        Assert.True(_vm.ExitCommand.CanExecute(null));
        Assert.False(_vm.SaveCommand.CanExecute(null));
        Assert.False(_vm.SaveAsCommand.CanExecute(null));
        Assert.False(_vm.CloseDocumentCommand.CanExecute(null));
        Assert.Equal("Mendikat TrailLab", _vm.Title);
    }

    [Fact]
    public void Save_is_enabled_only_when_the_active_document_has_changes()
    {
        Open(PathA);
        Assert.True(_vm.SaveAsCommand.CanExecute(null));
        Assert.False(_vm.SaveCommand.CanExecute(null));

        _vm.ActiveDocument!.Document.MarkChanged();

        Assert.True(_vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void Opening_a_second_file_keeps_the_first_one()
    {
        Open(PathA);
        var first = _vm.ActiveDocument;

        Open(PathB);

        Assert.Equal(2, _vm.Documents.Count);
        Assert.Same(first, _vm.Documents[0]);
        Assert.Equal("b.gpx", _vm.ActiveDocument!.FileName);
    }

    [Fact]
    public void Opening_an_already_open_file_activates_it_instead_of_duplicating()
    {
        Open(PathA);
        var first = _vm.ActiveDocument;
        Open(PathB);

        Open(PathA.ToUpperInvariant());

        Assert.Equal(2, _vm.Documents.Count);
        Assert.Same(first, _vm.ActiveDocument);
    }

    [Fact]
    public void Load_error_shows_a_spanish_message_and_adds_no_document()
    {
        Open(PathA);
        _store.LoadErrors[PathB] = DocumentLoadError.EmptyFile;

        Open(PathB);

        Assert.Single(_vm.Documents);
        var message = Assert.Single(_dialogs.Errors);
        Assert.Equal("El archivo «b.gpx» está vacío.", message);
        Assert.False(_vm.IsBusy);
        Assert.True(_vm.OpenGpxCommand.CanExecute(null));
    }

    [Fact]
    public void Title_and_tab_header_show_an_asterisk_for_unsaved_changes()
    {
        Open(PathA);
        Assert.Equal("a.gpx — Mendikat TrailLab", _vm.Title);

        _vm.ActiveDocument!.Document.MarkChanged();

        Assert.Equal("a.gpx *", _vm.ActiveDocument.Header);
        Assert.Equal("a.gpx * — Mendikat TrailLab", _vm.Title);
    }

    [Fact]
    public async Task Closing_an_unmodified_document_does_not_ask()
    {
        Open(PathA);

        Assert.True(await _vm.CloseDocumentAsync(_vm.ActiveDocument));

        Assert.Empty(_vm.Documents);
        Assert.Empty(_dialogs.SaveChangesQuestions);
        Assert.Null(_vm.ActiveDocument);
    }

    [Fact]
    public async Task Cancelling_close_keeps_the_document_and_its_changes()
    {
        Open(PathA);
        var document = _vm.ActiveDocument!;
        document.Document.MarkChanged();
        _dialogs.SaveChangesAnswer = SaveChangesChoice.Cancel;

        Assert.False(await _vm.CloseDocumentAsync(document));

        Assert.Single(_vm.Documents);
        Assert.True(document.IsDirty);
        Assert.Equal(["a.gpx"], _dialogs.SaveChangesQuestions);
    }

    [Fact]
    public async Task Discarding_closes_without_saving()
    {
        Open(PathA);
        _vm.ActiveDocument!.Document.MarkChanged();
        _dialogs.SaveChangesAnswer = SaveChangesChoice.Discard;

        Assert.True(await _vm.CloseDocumentAsync(_vm.ActiveDocument));

        Assert.Empty(_vm.Documents);
        Assert.Empty(_store.SavedPaths);
    }

    [Fact]
    public async Task Choosing_save_when_saving_is_unavailable_keeps_the_document_open()
    {
        Open(PathA);
        _vm.ActiveDocument!.Document.MarkChanged();
        _dialogs.SaveChangesAnswer = SaveChangesChoice.Save;
        _store.SavingSupported = false;

        Assert.False(await _vm.CloseDocumentAsync(_vm.ActiveDocument));

        Assert.Single(_vm.Documents);
        Assert.Single(_dialogs.Infos);
    }

    [Fact]
    public async Task Choosing_save_saves_then_closes_but_a_failed_save_keeps_it_open()
    {
        _store.SavingSupported = true;
        _dialogs.SaveChangesAnswer = SaveChangesChoice.Save;
        Open(PathA);
        var document = _vm.ActiveDocument!;
        document.Document.MarkChanged();

        _store.FailSaving = true;
        Assert.False(await _vm.CloseDocumentAsync(document));
        Assert.True(document.IsDirty);
        Assert.Equal(["No se pudo guardar el archivo «a.gpx». El archivo original no se ha modificado."], _dialogs.Errors);

        _store.FailSaving = false;
        Assert.True(await _vm.CloseDocumentAsync(document));
        Assert.Equal([PathA], _store.SavedPaths);
    }

    [Fact]
    public async Task Exit_is_cancelled_if_any_modified_document_is_cancelled()
    {
        Open(PathA);
        Open(PathB);
        _vm.Documents[1].Document.MarkChanged();
        _dialogs.SaveChangesAnswer = SaveChangesChoice.Cancel;

        Assert.False(await _vm.ConfirmCloseAllAsync());
        Assert.Equal(2, _vm.Documents.Count);

        _dialogs.SaveChangesAnswer = SaveChangesChoice.Discard;
        Assert.True(await _vm.ConfirmCloseAllAsync());
    }

    [Fact]
    public async Task Closing_the_active_document_activates_a_neighbour()
    {
        Open(PathA);
        Open(PathB);
        Open(@"C:\data\c.gpx");
        _vm.ActiveDocument = _vm.Documents[1];

        await _vm.CloseDocumentAsync(_vm.ActiveDocument);

        Assert.Equal("c.gpx", _vm.ActiveDocument!.FileName);
    }

    [Fact]
    public async Task Save_as_saves_to_the_chosen_path_and_clears_the_indicator()
    {
        _store.SavingSupported = true;
        Open(PathA);
        _vm.ActiveDocument!.Document.MarkChanged();
        _dialogs.SavePath = @"C:\data\new.gpx";

        Assert.True(await _vm.SaveActiveAsAsync());

        Assert.False(_vm.ActiveDocument.IsDirty);
        Assert.Equal("new.gpx", _vm.ActiveDocument.FileName);
        Assert.Equal("new.gpx — Mendikat TrailLab", _vm.Title);
    }

    [Fact]
    public void First_run_uses_ign_spain()
    {
        Assert.Equal("ign-es", _vm.SelectedMapProvider.Id);
        Assert.Equal(8, _vm.MapProviders.Count);
    }

    [Fact]
    public void Chosen_map_is_saved_and_restored_on_next_start()
    {
        _vm.SelectedMapProvider = _vm.MapProviders.Single(p => p.Id == "opentopomap");

        Assert.Equal("opentopomap", _settings.Current.Map.Provider);
        Assert.Equal("opentopomap", CreateViewModel().SelectedMapProvider.Id);
    }

    [Fact]
    public void Unknown_saved_map_falls_back_to_ign()
    {
        _settings.Current = new() { Map = new() { Provider = "thunderforest" } };

        Assert.Equal("ign-es", CreateViewModel().SelectedMapProvider.Id);
    }

    [Fact]
    public async Task Opening_can_be_cancelled_and_adds_no_document()
    {
        Open(PathA);
        _store.PendingLoad = new TaskCompletionSource();
        Assert.False(_vm.CancelOperationCommand.CanExecute(null));

        var opening = _vm.OpenPathAsync(PathB, DocumentFormat.Gpx);
        Assert.True(_vm.IsBusy);
        Assert.True(_vm.CanCancelOperation);
        Assert.True(_vm.CancelOperationCommand.CanExecute(null));

        _vm.CancelOperationCommand.Execute(null);
        await opening;

        var remaining = Assert.Single(_vm.Documents);
        Assert.Same(remaining, _vm.ActiveDocument);
        Assert.Equal("Apertura de b.gpx cancelada.", _vm.StatusMessage);
        Assert.False(_vm.IsBusy);
        Assert.False(_vm.CanCancelOperation);
        Assert.Null(_vm.Progress);
        Assert.Empty(_dialogs.Errors);
    }

    [Fact]
    public void Tile_failures_show_a_status_that_clears_when_tiles_load()
    {
        _vm.ReportMapTiles(failed: true);
        Assert.Equal("No se pudo cargar la cartografía «Mapa IGN (España)»: sin conexión o el servidor no responde.", _vm.MapStatus);

        _vm.ReportMapTiles(failed: false);
        Assert.Null(_vm.MapStatus);
    }

    [Fact]
    public void Pointer_position_updates_both_coordinate_texts()
    {
        _vm.UpdatePointer(new Mendikat.TrailLab.Domain.Geo.GeoPosition(43.373086111, -1.752777778));

        Assert.StartsWith("30T ", _vm.PointerUtm);
        Assert.Equal("43º 22' 23,11'' N 1º 45' 10,00'' W", _vm.PointerDms);

        _vm.UpdatePointer(null);
        Assert.Equal("—", _vm.PointerUtm);
    }

    [Fact]
    public void Opening_a_file_asks_the_map_to_fit_its_data_without_closing_others()
    {
        var requests = new List<MapRequest>();
        _vm.MapRequested += (_, r) => requests.Add(r);
        Open(PathA);

        Open(PathB);

        Assert.Equal(2, _vm.Documents.Count);
        Assert.Equal(2, requests.Count);
        Assert.IsType<MapRequest.ZoomTo>(requests[1]);
    }

    [Fact]
    public void Zoom_to_data_is_enabled_only_with_points_and_profile_is_not_available_yet()
    {
        Assert.False(_vm.ZoomToDataCommand.CanExecute(null));
        Assert.True(_vm.ZoomInCommand.CanExecute(null));

        Open(PathA);

        Assert.True(_vm.ZoomToDataCommand.CanExecute(null));
        Assert.False(_vm.ElevationProfileCommand.CanExecute(null));
    }

    [Fact]
    public void Each_document_gets_its_own_color()
    {
        Open(PathA);
        Open(PathB);

        Assert.NotEqual(_vm.Documents[0].ColorHex, _vm.Documents[1].ColorHex);
    }

    private MainViewModel CreateViewModel() =>
        new(_dialogs, _store, _settings, NullLogger<MainViewModel>.Instance);

    private void Open(string path)
    {
        _dialogs.OpenPaths.Enqueue(path);
        // The fake store completes synchronously, so the task is already finished.
        _vm.OpenAsync(DocumentFormat.Gpx).GetAwaiter().GetResult();
    }
}
