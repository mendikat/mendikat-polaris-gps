using Mendikat.TrailLab.App.Settings;

namespace Mendikat.TrailLab.App.ViewModels;

/// <summary>File › Settings: the application preferences the user can change.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private AppLanguage _language;
    private bool _showScaleBar;

    public SettingsViewModel(UserSettings settings)
    {
        _language = AppLanguage.FromCode(settings.Language);
        _showScaleBar = settings.Map.ShowScaleBar;
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

    /// <summary>The settings with the user's choices applied.</summary>
    public UserSettings ApplyTo(UserSettings settings) => settings with
    {
        Language = Language.Code,
        Map = settings.Map with { ShowScaleBar = ShowScaleBar },
    };
}
