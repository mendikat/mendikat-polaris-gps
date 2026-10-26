using Mendikat.TrailLab.App.Services;
using Mendikat.TrailLab.App.Settings;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Model;
using Mendikat.TrailLab.Gpx.Wpt;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mendikat.TrailLab.Tests.App;

public sealed class SettingsAndStoreTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("traillab-tests-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private JsonSettingsStore Settings(string name = "settings.json") =>
        new(Path.Combine(_directory, name), NullLogger<JsonSettingsStore>.Instance);

    [Fact]
    [Trait("Req", "MAP-03")]
    [Trait("Acc", "ACC-MAP-01")]
    public void Without_a_settings_file_the_default_map_is_ign()
    {
        Assert.Equal("ign-es", Settings().Load().Map.Provider);
    }

    [Fact]
    [Trait("Req", "MAP-04")]
    [Trait("Acc", "ACC-MAP-01")]
    public void Settings_survive_a_restart_as_json()
    {
        Assert.True(Settings().Save(new UserSettings { Map = new MapSettings { Provider = "opentopomap" } }));

        Assert.Equal("opentopomap", Settings().Load().Map.Provider);
        Assert.Contains("\"provider\": \"opentopomap\"", File.ReadAllText(Path.Combine(_directory, "settings.json")));
    }

    [Fact]
    [Trait("Req", "MAP-04")]
    public void Corrupt_settings_fall_back_to_defaults()
    {
        File.WriteAllText(Path.Combine(_directory, "settings.json"), "{ not json");

        Assert.Equal("ign-es", Settings().Load().Map.Provider);
    }

    [Fact]
    [Trait("Req", "GPX-01")]
    public async Task File_store_loads_gpx_with_content_and_warnings()
    {
        var document = await new FileDocumentStore().LoadAsync(
            Fixture.PathOf("gpx/gpx10.gpx"), DocumentFormat.Gpx, null, TestContext.Current.CancellationToken);

        Assert.Equal("gpx10.gpx", document.FileName);
        Assert.Single(document.Data.Tracks);
        Assert.Contains(document.Warnings, w => w.Kind == LoadWarningKind.Gpx10);
        Assert.False(document.IsDirty);
    }

    [Fact]
    [Trait("Req", "WPT-01")]
    public async Task File_store_loads_wpt()
    {
        var document = await new FileDocumentStore().LoadAsync(
            Fixture.PathOf("wpt/ozi-sample.wpt"), DocumentFormat.Wpt, null, TestContext.Current.CancellationToken);

        Assert.Equal(3, document.Data.Waypoints.Count);
        Assert.IsType<WptFormatState>(document.Data.FormatState);
    }

    [Theory]
    [Trait("Req", "APP-05")]
    [InlineData("missing.gpx", DocumentLoadError.FileNotFound)]
    [InlineData("gpx/blank.gpx", DocumentLoadError.EmptyFile)]
    [InlineData("gpx/malformed.gpx", DocumentLoadError.InvalidXml)]
    public async Task File_store_reports_typed_errors(string fixture, DocumentLoadError expected)
    {
        var ex = await Assert.ThrowsAsync<DocumentLoadException>(() =>
            new FileDocumentStore().LoadAsync(Fixture.PathOf(fixture), DocumentFormat.Gpx, null, TestContext.Current.CancellationToken));

        Assert.Equal(expected, ex.Error);
    }

    [Fact]
    [Trait("Req", "APP-05")]
    public async Task Zero_byte_file_is_reported_as_empty()
    {
        var path = Path.Combine(_directory, "empty.gpx");
        await File.WriteAllBytesAsync(path, [], TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<DocumentLoadException>(() =>
            new FileDocumentStore().LoadAsync(path, DocumentFormat.Gpx, null, TestContext.Current.CancellationToken));

        Assert.Equal(DocumentLoadError.EmptyFile, ex.Error);
    }

    [Theory]
    [Trait("Req", "APP-05")]
    [Trait("Req", "GPX-02")]
    [InlineData(DocumentLoadError.InvalidXml, 12, null, "El archivo «r.gpx» no es un XML válido, línea 12.")]
    [InlineData(DocumentLoadError.UnsupportedDatum, 2, "European 1950", "El archivo «r.gpx» usa el datum «European 1950», línea 2. Solo se admite WGS 84 y no se convierte automáticamente.")]
    [InlineData(DocumentLoadError.UnsupportedWptDialect, null, null, "El archivo «r.gpx» no corresponde a ningún formato WPT admitido (OziExplorer o CompeGPS / TwoNav).")]
    public void Load_errors_have_spanish_messages(DocumentLoadError error, int? line, string? detail, string expected)
    {
        var message = UserMessages.ForLoadError(new DocumentLoadException(error, detail, line), "r.gpx");

        Assert.Equal(expected, message);
    }

    [Fact]
    [Trait("Req", "GPX-06")]
    public void Warnings_have_spanish_messages()
    {
        Assert.Equal(
            "Marcas de tiempo sin zona horaria: 3. Se han interpretado como UTC.",
            UserMessages.ForWarning(new LoadWarning(LoadWarningKind.TimesWithoutZone, 3)));
    }

    [Fact]
    [Trait("Req", "WPT-01")]
    public void Source_format_names_the_wpt_dialect()
    {
        var data = new TrailData
        {
            FormatState = new WptFormatState { DialectId = "compegps", Encoding = WaypointFileReader.Windows1252, HeaderLines = [] },
        };

        Assert.Equal("WPT (CompeGPS / TwoNav)", UserMessages.SourceFormat(DocumentFormat.Wpt, data));
    }
}
