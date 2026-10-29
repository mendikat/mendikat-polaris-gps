using Mendikat.TrailLab.App.Resources;
using Mendikat.TrailLab.App.Settings;

namespace Mendikat.TrailLab.App.ViewModels;

/// <summary>File › Settings: application preferences.</summary>
public sealed partial class MainViewModel
{
    public RelayCommand SettingsCommand { get; private set; } = null!;

    /// <summary>Scale bar on the map (File › Settings › Display).</summary>
    public bool ShowScaleBar
    {
        get => _showScaleBar;
        private set => SetProperty(ref _showScaleBar, value);
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
        ShowScaleBar = updated.Map.ShowScaleBar;

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

    private void InitializeSettingsCommands() => SettingsCommand = new RelayCommand(ShowSettings);
}
