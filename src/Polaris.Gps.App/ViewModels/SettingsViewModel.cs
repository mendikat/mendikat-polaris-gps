using Polaris.Gps.App.Settings;
using Polaris.Gps.Mapping.Coordinates;

namespace Polaris.Gps.App.ViewModels;

/// <summary>File › Settings: the application preferences the user can change.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private AppLanguage _language;
    private bool _showScaleBar;
    private bool _showUtmGrid;
    private string _datum;

    public SettingsViewModel(UserSettings settings)
    {
        _language = AppLanguage.FromCode(settings.Language);
        _showScaleBar = settings.Map.ShowScaleBar;
        _showUtmGrid = settings.Map.ShowUtmGrid;
        _datum = CoordinateDatums.Name(CoordinateDatums.FromName(settings.Datum));
    }

    public IReadOnlyList<AppLanguage> Languages => AppLanguage.All;

    public AppLanguage Language
    {
        get => _language;
        set => SetProperty(ref _language, value);
    }

    /// <summary>Display: the scale bar of the map.</summary>
    public bool ShowScaleBar
    {
        get => _showScaleBar;
        set => SetProperty(ref _showScaleBar, value);
    }

    /// <summary>Display: the UTM grid over the map (off by default).</summary>
    public bool ShowUtmGrid
    {
        get => _showUtmGrid;
        set => SetProperty(ref _showUtmGrid, value);
    }

    /// <summary>"WGS84" and "ED50", as shown in the list.</summary>
    public IReadOnlyList<string> Datums { get; } = CoordinateDatums.All.Select(CoordinateDatums.Name).ToList();

    /// <summary>Coordinates: datum in which they are shown and typed (WGS84 by default).</summary>
    public string Datum
    {
        get => _datum;
        set => SetProperty(ref _datum, value);
    }

    /// <summary>The settings with the user's choices applied.</summary>
    public UserSettings ApplyTo(UserSettings settings) => settings with
    {
        Language = Language.Code,
        Datum = Datum,
        Map = settings.Map with { ShowScaleBar = ShowScaleBar, ShowUtmGrid = ShowUtmGrid },
    };
}
