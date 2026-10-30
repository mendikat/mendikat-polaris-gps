using System.Windows;
using Polaris.Gps.App.ViewModels;

namespace Polaris.Gps.App.Views;

/// <summary>Modal dialog for <see cref="GpsDeviceViewModel"/>; returns true when the user sends or opens.</summary>
public partial class GpsDeviceWindow : Window
{
    public GpsDeviceWindow(GpsDeviceViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) => DeviceBox.Focus();
    }

    private void OnAccept(object sender, RoutedEventArgs e) => DialogResult = true;
}
