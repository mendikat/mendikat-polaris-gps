using Mendikat.TrailLab.App.Services;
using Mendikat.TrailLab.App.Settings;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Model;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mendikat.TrailLab.Tests.App;

public sealed class SettingsAndStoreTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("traillab-tests-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private JsonSettingsStore Settings(string name = "settings.json") =>
        new(Path.Combine(_directory, name), NullLogger<JsonSettingsStore>.Instance);

    [Fact]
    public void Without_a_settings_file_the_default_map_is_ign()
    {
        Assert.Equal("ign-es", Settings().Load().Map.Provider);
    }

    [Fact]
    public void Settings_survive_a_restart_as_json()
    {
        Assert.True(Settings().Save(new UserSettings { Map = new MapSettings { Provider = "opentopomap" } }));

        Assert.Equal("opentopomap", Settings().Load().Map.Provider);
        Assert.Contains("\"provider\": \"opentopomap\"", File.ReadAllText(Path.Combine(_directory, "settings.json")));
    }

    [Fact]
    public void Corrupt_settings_fall_back_to_defaults()
    {
        File.WriteAllText(Path.Combine(_directory, "settings.json"), "{ not json");

        Assert.Equal("ign-es", Settings().Load().Map.Provider);
    }

    [Fact]
    public async Task File_store_loads_gpx_with_content_and_warnings()
    {
        var document = await new FileDocumentStore().LoadAsync(
            Fixture.PathOf("gpx/gpx10.gpx"), DocumentFormat.Gpx, null, TestContext.Current.CancellationToken);

        Assert.Equal("gpx10.gpx", document.FileName);
        Assert.Single(document.Data.Tracks);
        Assert.Contains(document.Warnings, w => w.Kind == LoadWarningKind.Gpx10);
        Assert.False(document.IsDirty);
    }

    [Theory]
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
    public async Task Zero_byte_file_is_reported_as_empty()
    {
        var path = Path.Combine(_directory, "empty.gpx");
        await File.WriteAllBytesAsync(path, [], TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<DocumentLoadException>(() =>
            new FileDocumentStore().LoadAsync(path, DocumentFormat.Gpx, null, TestContext.Current.CancellationToken));

        Assert.Equal(DocumentLoadError.EmptyFile, ex.Error);
    }

    [Theory]
    [InlineData(DocumentLoadError.InvalidXml, 12, null, "El archivo «r.gpx» no es un XML válido, línea 12.")]
    [InlineData(DocumentLoadError.InvalidCoordinate, 2, "lat=\"95\"", "Coordenadas no válidas en «r.gpx», línea 2: lat=\"95\".\nLa latitud debe estar entre −90 y 90 y la longitud entre −180 y 180.")]
    public void Load_errors_have_spanish_messages(DocumentLoadError error, int? line, string? detail, string expected)
    {
        var message = UserMessages.ForLoadError(new DocumentLoadException(error, detail, line), "r.gpx");

        Assert.Equal(expected, message);
    }

    [Fact]
    public void Warnings_have_spanish_messages()
    {
        Assert.Equal(
            "Marcas de tiempo sin zona horaria: 3. Se han interpretado como UTC.",
            UserMessages.ForWarning(new LoadWarning(LoadWarningKind.TimesWithoutZone, 3)));
    }
}
