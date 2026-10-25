using System.Windows;
using System.Windows.Input;
using Mendikat.TrailLab.App.ViewModels;

namespace Mendikat.TrailLab.App.Views;

/// <summary>Independent window with the elevation profile of one track or route (PROF-01).</summary>
public partial class ProfileWindow : Window
{
    private readonly ProfileViewModel _viewModel;

    public ProfileWindow(ProfileViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        Chart.Hovered += (_, distance) => _viewModel.Hover(distance);
        Chart.Clicked += (_, distance) => _viewModel.SelectAt(distance);
        viewModel.CloseRequested += (_, _) => Close();
        Loaded += (_, _) => Chart.Focus();
    }

    public ProfileViewModel ViewModel => _viewModel;

    /// <summary>Left/right arrows move the selection to the previous/next point.</summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key is Key.Left or Key.Right)
        {
            _viewModel.Step(e.Key == Key.Left ? -1 : 1);
            e.Handled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Detach();
        base.OnClosed(e);
    }
}
