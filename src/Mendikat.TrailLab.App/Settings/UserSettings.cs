using Mendikat.TrailLab.Mapping.Providers;

namespace Mendikat.TrailLab.App.Settings;

/// <summary>Per-user preferences kept between sessions. Stored as JSON; never contains secrets.</summary>
public sealed record UserSettings
{
    public MapSettings Map { get; init; } = new();

    /// <summary>Language of the user interface (<see cref="AppLanguage.Code"/>); applied when the application starts.</summary>
    public string Language { get; init; } = AppLanguage.DefaultCode;

    /// <summary>
    /// The user accepted the ERDAS ECW/JP2 end-user licence; until then ECW files and ECWP streams are not opened, as
    /// the SDK licence requires.
    /// </summary>
    public bool EcwLicenseAccepted { get; init; }
}

public sealed record MapSettings
{
    /// <summary>Selected map provider id (MAP-04). IGN Spain on first run (MAP-03).</summary>
    public string Provider { get; init; } = MapProviderCatalog.DefaultId;
}

public interface ISettingsStore
{
    /// <summary>Returns the saved settings, or defaults if there are none or they cannot be read.</summary>
    UserSettings Load();

    /// <returns>False if the settings could not be written (the error is logged).</returns>
    bool Save(UserSettings settings);
}
