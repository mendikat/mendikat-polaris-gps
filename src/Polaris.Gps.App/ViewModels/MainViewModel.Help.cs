using System.Globalization;
using System.IO;
using Polaris.Gps.App.Resources;
using Polaris.Gps.App.Services;
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

    /// <summary>Asks for the latest published version. Set at startup; without it the releases page is opened.</summary>
    public IUpdateChecker? UpdateChecker { get; init; }

    /// <summary>
    /// Help › Check for updates: compares this version with the latest published one. If there is a newer one, offers to
    /// open its page to download the setup program; if the check fails (offline), offers the page with all versions.
    /// </summary>
    public async Task CheckUpdatesAsync()
    {
        if (UpdateChecker is null)
        {
            _dialogs.OpenExternal(AboutViewModel.UpdatesUrl);
            return;
        }

        var current = GitHubUpdateChecker.TryParseVersion(AboutViewModel.Version) ?? new Version(0, 0, 0);
        StatusMessage = Strings.StatusCheckingUpdates;
        try
        {
            var latest = await UpdateChecker.LatestAsync(CancellationToken.None);
            _logger.LogInformation("Update check: running {Current}, latest {Latest}", current, latest.Version);
            if (latest.Version > current)
            {
                StatusMessage = string.Format(Strings.UpdateAvailableStatus, latest.Version.ToString(3));
                if (_dialogs.Confirm(string.Format(Strings.UpdateAvailable, latest.Version.ToString(3), current.ToString(3))))
                {
                    _dialogs.OpenExternal(latest.PageUrl);
                }
            }
            else
            {
                StatusMessage = Strings.StatusReady;
                _dialogs.ShowInfo(string.Format(Strings.UpdateNone, current.ToString(3)));
            }
        }
        catch (UpdateCheckException ex)
        {
            _logger.LogInformation("Update check failed: {Reason}", ex.Message);
            StatusMessage = Strings.StatusReady;
            if (_dialogs.Confirm(Strings.UpdateCheckFailed))
            {
                _dialogs.OpenExternal(AboutViewModel.UpdatesUrl);
            }
        }
    }

    private void InitializeHelpCommands()
    {
        HelpCommand = new RelayCommand(ShowHelp);
        DonateCommand = new RelayCommand(Donate);
        AboutCommand = new RelayCommand(ShowAbout);
        WebCommand = new RelayCommand(OpenWeb);
        CheckUpdatesCommand = new RelayCommand(_ => CheckUpdatesAsync());
    }
}
