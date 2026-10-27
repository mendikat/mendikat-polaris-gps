using Mendikat.TrailLab.App.Services;
using Mendikat.TrailLab.App.ViewModels;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Gpx;
using Mendikat.TrailLab.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mendikat.TrailLab.Tests.App;

/// <summary>Opening KML/KMZ/TCX as new GPX documents and the "Convert to GPX" tool, on real files (increment 10).</summary>
public sealed class ConvertFlowTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "traillab-convert-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly FakeDialogService _dialogs = new();
    private readonly MainViewModel _vm;

    public ConvertFlowTests()
    {
        Directory.CreateDirectory(_folder);
        _vm = new MainViewModel(_dialogs, new FileDocumentStore(), new FakeSettingsStore(), NullLogger<MainViewModel>.Instance);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>Copies a fixture of Fixtures/convert into the test folder.</summary>
    private string Copy(string fixture, string? name = null)
    {
        var path = Path.Combine(_folder, name ?? fixture);
        File.Copy(Fixture.PathOf("convert/" + fixture), path);
        return path;
    }

    private string[] GpxFiles() => Directory.GetFiles(_folder, "*.gpx").Select(Path.GetFileName).Order().ToArray()!;

    [Fact]
    [Trait("Req", "CONV-02")]
    public async Task A_kml_opens_as_a_new_unsaved_gpx_named_after_it()
    {
        var kml = Copy("sample.kml", "excursion.kml");

        await _vm.OpenPathAsync(kml, DocumentFormat.Gpx);

        var document = _vm.ActiveDocument!;
        Assert.Equal("excursion.gpx", document.FileName);
        Assert.True(document.Document.IsNew);
        Assert.Equal(kml, document.Document.ImportedFrom);
        Assert.Equal(4, document.Document.Data.Tracks.Count);
        Assert.Equal("KML (convertido a GPX)", document.FormatLabel);
        Assert.Equal("«excursion.kml» convertido a GPX como «excursion.gpx» (sin guardar).", _vm.StatusMessage);
        Assert.Contains(document.Warnings, w => w.StartsWith("Convertido desde KML a GPX.", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Req", "CONV-02")]
    public async Task Saving_a_converted_document_asks_where_and_never_touches_the_source()
    {
        var tcx = Copy("sample.tcx", "carrera.tcx");
        var original = File.ReadAllBytes(tcx);
        await _vm.OpenPathAsync(tcx, DocumentFormat.Gpx);
        _dialogs.SavePath = Path.Combine(_folder, "carrera.gpx");

        Assert.True(await _vm.SaveActiveAsync());

        Assert.Equal(Path.Combine(_folder, "carrera.gpx"), _dialogs.SuggestedSavePath); // next to the source
        Assert.Equal(original, File.ReadAllBytes(tcx));
        using var savedFile = File.OpenRead(_dialogs.SavePath!);
        var saved = new GpxReader().Read(savedFile, cancellationToken: TestContext.Current.CancellationToken).Data;
        Assert.Equal(_vm.ActiveDocument!.Document.Data.Tracks.Select(t => t.PointCount), saved.Tracks.Select(t => t.PointCount));
        Assert.False(_vm.ActiveDocument.Document.IsNew);
    }

    [Fact]
    [Trait("Req", "CONV-02")]
    public async Task Opening_the_same_source_again_activates_the_converted_document()
    {
        var kml = Copy("sample.kml");
        await _vm.OpenPathAsync(kml, DocumentFormat.Gpx);

        await _vm.OpenPathAsync(kml, DocumentFormat.Gpx);

        Assert.Single(_vm.Documents);
        Assert.Equal("«sample.kml» ya está abierto.", _vm.StatusMessage);
    }

    [Fact]
    [Trait("Req", "CONV-01")]
    public async Task An_unsupported_file_shows_a_clear_error_and_opens_nothing()
    {
        await _vm.OpenPathAsync(Copy("other.xml"), DocumentFormat.Gpx);

        Assert.Empty(_vm.Documents);
        Assert.Equal("El archivo «other.xml» no es GPX, KML, KMZ ni TCX (su elemento raíz es «html»).", Assert.Single(_dialogs.Errors));
    }

    [Fact]
    [Trait("Req", "CONV-06")]
    [Trait("Req", "CONV-07")]
    public async Task Convert_writes_one_valid_gpx_per_file_and_reports_errors_without_stopping()
    {
        var files = new[] { Copy("sample.kml", "a.kml"), Copy("broken.kmz", "b.kmz"), Copy("sample.tcx", "c.tcx") };
        var dialog = new ConvertViewModel(_dialogs, files);

        await dialog.ConvertAsync();

        Assert.Equal(["a.gpx", "c.gpx"], GpxFiles());
        Assert.Equal((2, 0, 1), (dialog.Converted, dialog.Skipped, dialog.Failed));
        var lines = dialog.Report.Split(Environment.NewLine);
        Assert.Equal("✓ a.kml → a.gpx: tracks 4, puntos 13, waypoints 2.", lines[0]);
        Assert.StartsWith("    · Elementos de KML sin equivalente en GPX", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("✗ El archivo «b.kmz» no es un KMZ válido", lines[2], StringComparison.Ordinal);
        Assert.Equal("Convertidos: 2 · Omitidos: 0 · Con error: 1.", lines[^1]);
        Assert.Equal(1, dialog.Progress);

        // The same GPX as opening the file and saving it (CONV-07).
        await _vm.OpenPathAsync(files[2], DocumentFormat.Gpx);
        Assert.Equal(
            TrailFileWriter.Serialize(_vm.ActiveDocument!.Document.Data, DocumentFormat.Gpx),
            File.ReadAllBytes(Path.Combine(_folder, "c.gpx")));
    }

    [Theory]
    [Trait("Req", "CONV-06")]
    [InlineData(ExistingFileAction.Rename, new[] { "ruta (2).gpx", "ruta.gpx" }, "viejo")]
    [InlineData(ExistingFileAction.Skip, new[] { "ruta.gpx" }, "viejo")]
    [InlineData(ExistingFileAction.Overwrite, new[] { "ruta.gpx" }, "nuevo")]
    public async Task Existing_gpx_files_are_renamed_skipped_or_replaced(ExistingFileAction action, string[] expected, string content)
    {
        var kml = Copy("sample.kml", "ruta.kml");
        File.WriteAllText(Path.Combine(_folder, "ruta.gpx"), "viejo");
        var dialog = new ConvertViewModel(_dialogs, [kml]);
        dialog.Existing = dialog.ExistingOptions.Single(o => o.Action == action);

        await dialog.ConvertAsync();

        Assert.Equal(expected, GpxFiles());
        var old = File.ReadAllText(Path.Combine(_folder, "ruta.gpx"));
        Assert.Equal(content, old == "viejo" ? "viejo" : "nuevo");
    }

    [Fact]
    [Trait("Req", "CONV-06")]
    public async Task Gpx_can_go_to_another_folder_and_gpx_sources_are_not_converted()
    {
        var target = Directory.CreateDirectory(Path.Combine(_folder, "salida")).FullName;
        var gpx = Path.Combine(_folder, "ya.gpx");
        File.WriteAllText(gpx, "<gpx version=\"1.1\" creator=\"t\" xmlns=\"http://www.topografix.com/GPX/1/1\"/>");
        _dialogs.Folder = target;
        var dialog = new ConvertViewModel(_dialogs, [Copy("sample.tcx"), gpx]);

        dialog.BrowseCommand.Execute(null);
        await dialog.ConvertAsync();

        Assert.False(dialog.UseSourceFolder);
        Assert.True(File.Exists(Path.Combine(target, "sample.gpx")));
        Assert.Equal("– ya.gpx: ya es un archivo GPX; no se convierte.", dialog.Report.Split(Environment.NewLine)[^2]);
        Assert.Equal((1, 1, 0), (dialog.Converted, dialog.Skipped, dialog.Failed));
    }

    [Fact]
    [Trait("Req", "CONV-06")]
    public void Another_folder_must_be_chosen_before_converting()
    {
        var dialog = new ConvertViewModel(_dialogs, [Copy("sample.kml")]) { UseOtherFolder = true };

        Assert.False(dialog.CanConvert);
        dialog.TargetFolder = _folder;
        Assert.True(dialog.CanConvert);
    }

    [Fact]
    [Trait("Req", "CONV-06")]
    public async Task Stopping_leaves_only_complete_gpx_files()
    {
        var files = Enumerable.Range(1, 20).Select(i => Copy("sample.tcx", $"r{i:00}.tcx")).ToList();
        var dialog = new ConvertViewModel(_dialogs, files);

        var conversion = dialog.ConvertAsync();
        dialog.Stop();
        await conversion;

        Assert.Contains("Conversión detenida.", dialog.Report, StringComparison.Ordinal);
        Assert.True(GpxFiles().Length < 20);
        Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
        Assert.All(GpxFiles(), f => new GpxReader().Read(new MemoryStream(File.ReadAllBytes(Path.Combine(_folder, f))), cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(dialog.IsConverting);
    }

    [Fact]
    [Trait("Req", "CONV-06")]
    public void Files_can_be_added_and_removed_but_not_twice()
    {
        var kml = Copy("sample.kml");
        var tcx = Copy("sample.tcx");
        var dialog = new ConvertViewModel(_dialogs, [kml]);
        _dialogs.ConvertPaths.Enqueue([kml.ToUpperInvariant(), tcx]);

        dialog.AddFilesCommand.Execute(null);
        Assert.Equal([kml, tcx], dialog.Files);

        dialog.SelectedFile = kml;
        dialog.RemoveFileCommand.Execute(null);
        Assert.Equal([tcx], dialog.Files);
    }

    [Fact]
    [Trait("Req", "CONV-06")]
    public void The_tool_shows_the_dialog_with_the_chosen_files_and_reports_in_the_status_bar()
    {
        var kml = Copy("sample.kml");
        ConvertViewModel? shown = null;
        _dialogs.ToolDialog = vm =>
        {
            shown = (ConvertViewModel)vm;
            shown.ConvertAsync().GetAwaiter().GetResult();
            return false;
        };

        _vm.ConvertToGpxCommand.Execute(null); // the user cancels the file selection
        Assert.Null(shown);

        _dialogs.ConvertPaths.Enqueue([kml]);
        _vm.ConvertToGpxCommand.Execute(null);

        Assert.Equal([kml], shown!.Files);
        Assert.Equal("Convertir a GPX: 1 convertidos, 0 omitidos, 0 con error.", _vm.StatusMessage);
        Assert.Empty(_vm.Documents);
    }
}
