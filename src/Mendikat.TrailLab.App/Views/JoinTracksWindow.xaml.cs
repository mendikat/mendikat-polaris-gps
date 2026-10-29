using System.Windows;
using Mendikat.TrailLab.App.ViewModels;

namespace Mendikat.TrailLab.App.Views;

/// <summary>Modal dialog for <see cref="JoinTracksViewModel"/>; returns true when the user joins.</summary>
public partial class JoinTracksWindow : Window
{
    public JoinTracksWindow(JoinTracksViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) => TrackList.Focus();
    }

    private void OnApply(object sender, RoutedEventArgs e) => DialogResult = true;
}
