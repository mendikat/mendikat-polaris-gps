using Polaris.Gps.App.ViewModels;
using Polaris.Gps.Domain.Documents;
using Polaris.Gps.Gpx;
using Polaris.Gps.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Polaris.Gps.Tests.App;

/// <summary>Save and Save as through the view model (increment 4).</summary>
public sealed class SaveFlowTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("polarisgps-saveflow-").FullName;
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeDocumentStore _store = new() { SavingSupported = true };
    private readonly MainViewModel _vm;

    public SaveFlowTests()
    {
        _vm = new MainViewModel(_dialogs, _store, new FakeSettingsStore(), NullLogger<MainViewModel>.Instance);
        _dialogs.OpenPaths.Enqueue(Path.Combine(_directory, "ruta.gpx"));
        _vm.OpenAsync(DocumentFormat.Gpx).GetAwaiter().GetResult();
        _vm.ActiveDocument!.Document.MarkChanged();
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task Save_writes_to_the_origin_and_clears_the_indicator()
    {
        Assert.True(await _vm.SaveActiveAsync());

        Assert.Equal([Path.Combine(_directory, "ruta.gpx")], _store.SavedPaths);
        Assert.False(_vm.ActiveDocument!.IsDirty);
        Assert.Equal("Guardado: ruta.gpx", _vm.StatusMessage);
    }

    [Fact]
    public async Task Save_as_forces_the_extension_of_the_format()
    {
        _dialogs.SavePath = Path.Combine(_directory, "copia.txt");

        Assert.True(await _vm.SaveActiveAsAsync());

        Assert.Equal(Path.Combine(_directory, "copia.txt.gpx"), _store.SavedPaths.Single());
        Assert.Equal("copia.txt.gpx", _vm.ActiveDocument!.FileName);
    }

    [Fact]
    public async Task Save_as_asks_before_overwriting_a_file_whose_name_was_completed()
    {
        File.WriteAllText(Path.Combine(_directory, "existe.gpx"), "x");
        _dialogs.SavePath = Path.Combine(_directory, "existe");
        _dialogs.ConfirmAnswer = false;

        Assert.False(await _vm.SaveActiveAsAsync());

        Assert.Empty(_store.SavedPaths);
        Assert.Contains("existe.gpx", _dialogs.Confirmations.Single());
    }

    [Fact]
    public async Task Save_issues_are_confirmed_and_cancelling_keeps_the_changes()
    {
        _store.Issues.Add(new SaveIssue(SaveIssueKind.GpxVersionUpgrade));
        _dialogs.ConfirmAnswer = false;

        Assert.False(await _vm.SaveActiveAsync());

        Assert.Empty(_store.SavedPaths);
        Assert.True(_vm.ActiveDocument!.IsDirty);
        Assert.Contains("se guardará como GPX 1.1", _dialogs.Confirmations.Single());
    }

    [Fact]
    public async Task The_gpx_10_upgrade_is_asked_only_once_per_document()
    {
        _store.Issues.Add(new SaveIssue(SaveIssueKind.GpxVersionUpgrade));

        Assert.True(await _vm.SaveActiveAsync());
        _vm.ActiveDocument!.Document.MarkChanged();
        Assert.True(await _vm.SaveActiveAsync());

        Assert.Single(_dialogs.Confirmations);
        Assert.Equal(2, _store.SavedPaths.Count);
    }

    [Theory]
    [InlineData("verification", "no superó la verificación")]
    [InlineData("access", "No hay permiso para escribir")]
    [InlineData("io", "No se pudo guardar el archivo")]
    public async Task Save_failures_keep_the_document_modified_and_explain_why(string failure, string expected)
    {
        _store.FailSaving = true;
        _store.SaveFailure = failure switch
        {
            "verification" => new SaveVerificationException("x"),
            "access" => new UnauthorizedAccessException("x"),
            _ => new IOException("x"),
        };

        Assert.False(await _vm.SaveActiveAsync());

        Assert.True(_vm.ActiveDocument!.IsDirty);
        Assert.Contains(expected, _dialogs.Errors.Single());
        Assert.Contains("El archivo original no se ha modificado", _dialogs.Errors.Single());
        Assert.False(_vm.IsBusy);
    }

    [Fact]
    public async Task Choosing_save_when_closing_saves_and_closes()
    {
        _dialogs.SaveChangesAnswer = Polaris.Gps.App.Services.SaveChangesChoice.Save;

        Assert.True(await _vm.CloseDocumentAsync(_vm.ActiveDocument));

        Assert.Single(_store.SavedPaths);
        Assert.Empty(_vm.Documents);
    }
}
