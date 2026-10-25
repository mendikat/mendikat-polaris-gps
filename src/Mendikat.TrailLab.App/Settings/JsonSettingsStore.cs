using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Mendikat.TrailLab.App.Settings;

/// <summary>Settings in a JSON file, by default %APPDATA%\Mendikat\TrailLab\settings.json.</summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _path;
    private readonly ILogger _logger;

    public JsonSettingsStore(string path, ILogger<JsonSettingsStore> logger)
    {
        _path = path;
        _logger = logger;
    }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mendikat", "TrailLab", "settings.json");

    public UserSettings Load()
    {
        if (!File.Exists(_path))
        {
            return new UserSettings();
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
