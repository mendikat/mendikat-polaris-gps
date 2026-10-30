using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Polaris.Gps.App.Settings;

/// <summary>Settings in a JSON file, by default %APPDATA%\Polaris GPS\settings.json.</summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _path;
    private readonly string? _legacyPath;
    private readonly ILogger _logger;

    /// <param name="legacyPath">Settings file of an older version; read once if <paramref name="path"/> does not exist yet.</param>
    public JsonSettingsStore(string path, string? legacyPath, ILogger<JsonSettingsStore> logger)
    {
        _path = path;
        _legacyPath = legacyPath;
        _logger = logger;
    }

    public JsonSettingsStore(string path, ILogger<JsonSettingsStore> logger)
        : this(path, null, logger)
    {
    }

    /// <summary>
    /// Interface language while the user has no settings file yet (first run): the one chosen in the installer
    /// (<see cref="InstalledLanguage"/>). Null or unknown means Spanish.
    /// </summary>
    public string? DefaultLanguage { get; init; }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Polaris GPS", "settings.json");

    /// <summary>Where the application kept its settings when it was called Mendikat TrailLab (up to 0.1.1).</summary>
    public static string LegacyPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mendikat", "TrailLab", "settings.json");

    public UserSettings Load()
    {
        MigrateLegacy();
        if (!File.Exists(_path))
        {
            return new UserSettings { Language = AppLanguage.FromCode(DefaultLanguage).Code };
        }

        try
        {
            return JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(_path), Options) ?? new UserSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Settings could not be read; using defaults");
            return new UserSettings();
        }
    }

    /// <summary>Copies the old settings file to the new path once; the old file is left as it is.</summary>
    private void MigrateLegacy()
    {
        if (_legacyPath is null || File.Exists(_path) || !File.Exists(_legacyPath))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.Copy(_legacyPath, _path);
            _logger.LogInformation("Settings migrated from the Mendikat TrailLab folder");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Old settings could not be migrated");
        }
    }

    public bool Save(UserSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, Options));
            File.Move(temporary, _path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Settings could not be saved");
            return false;
        }
    }
}
