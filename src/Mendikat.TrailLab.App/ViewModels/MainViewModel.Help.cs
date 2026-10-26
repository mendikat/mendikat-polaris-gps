using System.IO;
using Mendikat.TrailLab.App.Resources;
using Microsoft.Extensions.Logging;

namespace Mendikat.TrailLab.App.ViewModels;

/// <summary>Help menu (APP-09): user guide, donation, mendikat.net, updates and "About".</summary>
public sealed partial class MainViewModel
{
    /// <summary>The user guide, copied next to the executable (Help\ayuda.html).</summary>
    public static string HelpFilePath => Path.Combine(AppContext.BaseDirectory, "Help", "ayuda.html");

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
