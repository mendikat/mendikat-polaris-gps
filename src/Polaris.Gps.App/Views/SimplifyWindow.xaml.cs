using System.ComponentModel;
using System.Windows;
using Polaris.Gps.App.ViewModels;

namespace Polaris.Gps.App.Views;

/// <summary>Modal dialog for <see cref="SimplifyViewModel"/>; returns true when the user applies. Closing it stops any computation.</summary>
public partial class SimplifyWindow : Window
{
    private readonly SimplifyViewModel _viewModel;

    public SimplifyWindow(SimplifyViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ToleranceBox.Focus();
            ToleranceBox.SelectAll();
        };
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DialogResult != true)
        {
            _viewModel.Cancel();
        }

        base.OnClosing(e);
    }

    private void OnApply(object sender, RoutedEventArgs e) => DialogResult = true;
}
