using Polaris.Gps.App.Services;
using Polaris.Gps.App.Settings;
using Polaris.Gps.Domain.Documents;
using Polaris.Gps.Domain.Model;
using Microsoft.Extensions.Logging.Abstractions;

namespace Polaris.Gps.Tests.App;

public sealed class SettingsAndStoreTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("polarisgps-tests-").FullName;

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
    public void Settings_of_mendikat_traillab_are_copied_once_to_the_new_folder()
    {
        var legacy = Path.Combine(_directory, "Mendikat", "TrailLab", "settings.json");
        var path = Path.Combine(_directory, "Polaris GPS", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacy)!);
        File.WriteAllText(legacy, "{ \"map\": { \"provider\": \"opentopomap\" } }");
        var store = new JsonSettingsStore(path, legacy, NullLogger<JsonSettingsStore>.Instance);

        Assert.Equal("opentopomap", store.Load().Map.Provider);
        Assert.True(File.Exists(path));
        Assert.True(File.Exists(legacy)); // the old file is left as it is

        Assert.True(store.Save(new UserSettings { Map = new MapSettings { Provider = "osm" } }));
        File.WriteAllText(legacy, "{ \"map\": { \"provider\": \"ign-es\" } }");
        Assert.Equal("osm", store.Load().Map.Provider); // only when the new file does not exist
    }

    [Fact]
    public void Corrupt_settings_fall_back_to_defaults()
    {
        File.WriteAllText(Path.Combine(_directory, "settings.json"), "{ not json");

        Assert.Equal("ign-es", Settings().Load().Map.Provider);
    }

    [Theory]
    [InlineData("eu", "eu")]
    [InlineData("FR", "fr")]
    [InlineData("de", "es")]
    [InlineData(null, "es")]
    public void Installer_language_is_used_until_the_user_has_settings(string? installed, string expected)
    {
        var path = Path.Combine(_directory, "settings.json");
        var store = new JsonSettingsStore(path, NullLogger<JsonSettingsStore>.Instance) { DefaultLanguage = installed };

        Assert.Equal(expected, store.Load().Language);
    }

    [Fact]
    public void Saved_language_wins_over_the_installer_language()
    {
        var path = Path.Combine(_directory, "settings.json");
        var store = new JsonSettingsStore(path, NullLogger<JsonSettingsStore>.Instance) { DefaultLanguage = "pt" };
        Assert.True(store.Save(new UserSettings { Language = "ca" }));

        Assert.Equal("ca", store.Load().Language);
    }

    [Theory]
    [InlineData("eu", "eu")]
    [InlineData(" Ca ", "ca")]
    [InlineData("en", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Installer_language_must_be_an_application_language(string? value, string? expected) =>
        Assert.Equal(expected, InstalledLanguage.Validate(value));

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
