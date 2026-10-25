using System.ComponentModel;
using System.Windows;
using Mendikat.TrailLab.App.ViewModels;

namespace Mendikat.TrailLab.App.Views;

/// <summary>Modal dialog for <see cref="DemViewModel"/>; returns true when the user applies. Closing it stops any download.</summary>
public partial class DemWindow : Window
{
    private readonly DemViewModel _viewModel;

    public DemWindow(DemViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.CancelQuery();
        base.OnClosing(e);
    }

    private void OnApply(object sender, RoutedEventArgs e) => DialogResult = true;
}
