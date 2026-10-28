using System.Windows;
using Mendikat.TrailLab.App.ViewModels;

namespace Mendikat.TrailLab.App.Views;

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
