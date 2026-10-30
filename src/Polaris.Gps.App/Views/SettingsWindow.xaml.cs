using System.Windows;
using Polaris.Gps.App.ViewModels;

namespace Polaris.Gps.App.Views;

/// <summary>File › Settings, for <see cref="SettingsViewModel"/>; returns true when the user accepts.</summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private void OnAccept(object sender, RoutedEventArgs e) => DialogResult = true;
}
