using System.Windows;
using Polaris.Gps.App.ViewModels;

namespace Polaris.Gps.App.Views;

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
