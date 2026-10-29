using System.Windows;
using Mendikat.TrailLab.App.ViewModels;

namespace Mendikat.TrailLab.App.Views;

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
