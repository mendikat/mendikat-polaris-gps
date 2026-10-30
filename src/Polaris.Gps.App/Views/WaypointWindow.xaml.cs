using System.Windows;
using Polaris.Gps.App.ViewModels;

namespace Polaris.Gps.App.Views;

/// <summary>Add or edit a waypoint, for <see cref="WaypointViewModel"/>; returns true when the user accepts.</summary>
public partial class WaypointWindow : Window
{
    public WaypointWindow(WaypointViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) => NameBox.SelectAll();
    }

    private void OnAccept(object sender, RoutedEventArgs e) => DialogResult = true;
}
