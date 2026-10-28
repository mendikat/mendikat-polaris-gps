using Mendikat.TrailLab.App.Settings;

namespace Mendikat.TrailLab.App.ViewModels;

/// <summary>File › Settings: the application preferences the user can change.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private AppLanguage _language;

    public SettingsViewModel(UserSettings settings)
    {
        _language = AppLanguage.FromCode(settings.Language);
    }

    public IReadOnlyList<AppLanguage> Languages => AppLanguage.All;

    public AppLanguage Language
    {
        get => _language;
        set => SetProperty(ref _language, value);
    }

    /// <summary>The settings with the user's choices applied.</summary>
    public UserSettings ApplyTo(UserSettings settings) => settings with { Language = Language.Code };
}
