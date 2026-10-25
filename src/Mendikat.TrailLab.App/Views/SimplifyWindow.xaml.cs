using System.Windows;
using Mendikat.TrailLab.App.ViewModels;

namespace Mendikat.TrailLab.App.Views;

/// <summary>Modal dialog for <see cref="SimplifyViewModel"/>; returns true when the user applies.</summary>
public partial class SimplifyWindow : Window
{
    public SimplifyWindow(SimplifyViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ToleranceBox.Focus();
            ToleranceBox.SelectAll();
        };
    }

    private void OnApply(object sender, RoutedEventArgs e) => DialogResult = true;
}
