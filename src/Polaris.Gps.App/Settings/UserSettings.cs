using Polaris.Gps.Mapping.Providers;

namespace Polaris.Gps.App.Settings;

/// <summary>Per-user preferences kept between sessions. Stored as JSON; never contains secrets.</summary>
public sealed record UserSettings
{
    public MapSettings Map { get; init; } = new();

    public DrawingSettings Drawing { get; init; } = new();

    /// <summary>Language of the user interface (<see cref="AppLanguage.Code"/>); applied when the application starts.</summary>
    public string Language { get; init; } = AppLanguage.DefaultCode;

    /// <summary>
    /// The user accepted the ERDAS ECW/JP2 end-user licence; until then ECW files and ECWP streams are not opened, as
    /// the SDK licence requires.
    /// </summary>
    public bool EcwLicenseAccepted { get; init; }

    /// <summary>Datum of the coordinates shown and typed: "WGS84" (default) or "ED50" (<see cref="Mapping.Coordinates.CoordinateDatums"/>).</summary>
    public string Datum { get; init; } = "WGS84";
}

public sealed record MapSettings
{
    /// <summary>Selected map provider id (MAP-04). IGN Spain on first run (MAP-03).</summary>
    public string Provider { get; init; } = MapProviderCatalog.DefaultId;

    /// <summary>Scale bar in the lower right corner of the map; shown by default.</summary>
    public bool ShowScaleBar { get; init; } = true;

    /// <summary>UTM grid over the map, in the coordinate datum; hidden by default.</summary>
    public bool ShowUtmGrid { get; init; }

    /// <summary>Colour of the tracks on the map: "None" (document colour, default), "Elevation", "Slope" or "Speed".</summary>
    public string TrackColoring { get; init; } = "None";
}

/// <summary>Drawing options (toolbar), remembered between sessions.</summary>
public sealed record DrawingSettings
{
    /// <summary>Join new points along paths with BRouter; off by default (needs Internet).</summary>
    public bool FollowPaths { get; init; }

    /// <summary>BRouter profile id; mountain hiking by default.</summary>
    public string RoutingProfile { get; init; } = "hiking-mountain";

    /// <summary>Snap a click to a nearby existing point; on by default.</summary>
    public bool SnapToPoints { get; init; } = true;

    /// <summary>Terrain elevation for new points; on by default.</summary>
    public bool AutoElevation { get; init; } = true;
}

public interface ISettingsStore
{
    /// <summary>Returns the saved settings, or defaults if there are none or they cannot be read.</summary>
    UserSettings Load();

    /// <returns>False if the settings could not be written (the error is logged).</returns>
    bool Save(UserSettings settings);
}
