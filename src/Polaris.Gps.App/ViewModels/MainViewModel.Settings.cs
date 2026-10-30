using Polaris.Gps.App.Resources;
using Polaris.Gps.App.Settings;
using Polaris.Gps.Mapping.Coordinates;

namespace Polaris.Gps.App.ViewModels;

/// <summary>File › Settings: application preferences.</summary>
public sealed partial class MainViewModel
{
    private bool _showUtmGrid;
    private CoordinateDatum _coordinateDatum;

    public RelayCommand SettingsCommand { get; private set; } = null!;

    /// <summary>Scale bar on the map (File › Settings › Display).</summary>
    public bool ShowScaleBar
    {
        get => _showScaleBar;
        private set => SetProperty(ref _showScaleBar, value);
    }

    /// <summary>UTM grid over the map, in <see cref="CoordinateDatum"/> (File › Settings › Display; off by default).</summary>
    public bool ShowUtmGrid
    {
        get => _showUtmGrid;
        private set => SetProperty(ref _showUtmGrid, value);
    }

    /// <summary>
    /// Datum of the coordinates shown and typed (File › Settings › Coordinates): status bar, points list, point detail,
    /// waypoints and UTM grid. Positions are always kept and saved in WGS84.
    /// </summary>
    public CoordinateDatum CoordinateDatum
    {
        get => _coordinateDatum;
        private set
        {
            if (SetProperty(ref _coordinateDatum, value))
            {
                foreach (var document in Documents)
                {
                    document.Datum = value;
                }
            }
        }
    }

    /// <summary>
    /// Opens the settings window and saves what the user accepts. The scale bar changes at once. The texts are read when the windows are built, so a
    /// new language is applied the next time the application starts; the user is told so in that language.
    /// </summary>
    public void ShowSettings()
    {
        var current = _settings.Load();
        var dialog = new SettingsViewModel(current);
        if (!_dialogs.ShowToolDialog(dialog))
        {
            return;
        }

        var updated = dialog.ApplyTo(current);
        if (updated == current)
        {
            return;
        }

        // Applied at once, even if it cannot be saved for the next session.
        ApplyDisplaySettings(updated);

        if (!_settings.Save(updated))
        {
            _dialogs.ShowError(Strings.StatusMapSaveSettingsFailed);
            return;
        }

        if (updated.Language != current.Language)
        {
            var culture = AppLanguage.FromCode(updated.Language).Culture;
            _dialogs.ShowInfo(Strings.ResourceManager.GetString(nameof(Strings.SettingsLanguageChanged), culture) ?? Strings.SettingsLanguageChanged);
        }
    }

    /// <summary>The settings that change the window at once: scale bar, UTM grid and coordinate datum.</summary>
    private void ApplyDisplaySettings(UserSettings settings)
    {
        ShowScaleBar = settings.Map.ShowScaleBar;
        ShowUtmGrid = settings.Map.ShowUtmGrid;
        CoordinateDatum = CoordinateDatums.FromName(settings.Datum);
        PointerDatum = CoordinateDatums.Name(CoordinateDatum);
    }

    private void InitializeSettingsCommands() => SettingsCommand = new RelayCommand(ShowSettings);
}
