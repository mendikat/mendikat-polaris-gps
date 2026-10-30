using System.Globalization;
using System.IO;
using Polaris.Gps.App.Resources;
using Polaris.Gps.App.Settings;
using Microsoft.Extensions.Logging;

namespace Polaris.Gps.App.ViewModels;

/// <summary>Help menu (APP-09): user guide, donation, mendikat.net, updates and "About".</summary>
public sealed partial class MainViewModel
{
    /// <summary>The user guide in the language of the interface, copied next to the executable.</summary>
    public static string HelpFilePath => HelpFileFor(CultureInfo.CurrentUICulture);

    /// <summary>
    /// Help\ayuda.{code}.html for a translated language (ca, eu, fr, pt), Help\ayuda.html for Spanish, or for a language
    /// whose guide is missing.
    /// </summary>
    public static string HelpFileFor(CultureInfo culture)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Help");
        var code = AppLanguage.FromCode(culture.TwoLetterISOLanguageName).Code;
        var translated = Path.Combine(folder, $"ayuda.{code}.html");
        return code != AppLanguage.DefaultCode && File.Exists(translated) ? translated : Path.Combine(folder, "ayuda.html");
    }

    public RelayCommand HelpCommand { get; private set; } = null!;

    public RelayCommand DonateCommand { get; private set; } = null!;

    public RelayCommand AboutCommand { get; private set; } = null!;

    public RelayCommand WebCommand { get; private set; } = null!;

    public RelayCommand CheckUpdatesCommand { get; private set; } = null!;

    /// <summary>Opens the user guide in the default browser; works offline.</summary>
    public void ShowHelp()
    {
        if (!File.Exists(HelpFilePath))
        {
            _logger.LogError("Help file not found: {FileName}", Path.GetFileName(HelpFilePath));
            _dialogs.ShowError(Strings.ErrorHelpNotFound);
            return;
        }

        _dialogs.OpenExternal(HelpFilePath);
    }

    /// <summary>Opens the PayPal donation page of Mendikat in the browser.</summary>
    public void Donate() => _dialogs.OpenExternal(AboutViewModel.DonateUrl);

    public void ShowAbout() => _dialogs.ShowAbout(new AboutViewModel());

    /// <summary>Opens https://www.mendikat.net in the browser.</summary>
    public void OpenWeb() => _dialogs.OpenExternal(AboutViewModel.WebUrl);

    /// <summary>Opens the page with the latest version (for now the mendikat.net home page).</summary>
    public void CheckUpdates() => _dialogs.OpenExternal(AboutViewModel.UpdatesUrl);

    private void InitializeHelpCommands()
    {
        HelpCommand = new RelayCommand(ShowHelp);
        DonateCommand = new RelayCommand(Donate);
        AboutCommand = new RelayCommand(ShowAbout);
        WebCommand = new RelayCommand(OpenWeb);
        CheckUpdatesCommand = new RelayCommand(CheckUpdates);
    }
}
