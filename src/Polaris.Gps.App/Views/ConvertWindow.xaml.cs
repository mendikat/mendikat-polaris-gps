using System.ComponentModel;
using System.Windows;
using Polaris.Gps.App.ViewModels;

namespace Polaris.Gps.App.Views;

/// <summary>Modal dialog for <see cref="ConvertViewModel"/>. Closing it stops the conversion after the current file.</summary>
public partial class ConvertWindow : Window
{
    private readonly ConvertViewModel _viewModel;

    public ConvertWindow(ConvertViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.Stop();
        base.OnClosing(e);
    }
}
