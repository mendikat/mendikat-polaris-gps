using System.Windows;
using System.Windows.Navigation;
using Mendikat.TrailLab.App.ViewModels;

namespace Mendikat.TrailLab.App.Views;

/// <summary>"About" window (APP-09): version, author, links, the donation button and "check for updates".</summary>
public partial class AboutWindow : Window
{
    private readonly Action<string> _openExternal;

    /// <param name="openExternal">Opens a web page or mail address with the default program.</param>
    public AboutWindow(AboutViewModel viewModel, Action<string> openExternal)
    {
        _openExternal = openExternal;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void OnNavigate(object sender, RequestNavigateEventArgs e)
    {
        _openExternal(e.Uri.OriginalString);
        e.Handled = true;
    }

    private void OnDonate(object sender, RoutedEventArgs e) => _openExternal(AboutViewModel.DonateUrl);

    private void OnCheckUpdates(object sender, RoutedEventArgs e) => _openExternal(AboutViewModel.UpdatesUrl);
}
