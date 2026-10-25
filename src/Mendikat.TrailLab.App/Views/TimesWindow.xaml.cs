using System.Windows;
using Mendikat.TrailLab.App.ViewModels;

namespace Mendikat.TrailLab.App.Views;

/// <summary>Modal dialog for <see cref="TimesViewModel"/>; returns true when the user applies.</summary>
public partial class TimesWindow : Window
{
    public TimesWindow(TimesViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private void OnApply(object sender, RoutedEventArgs e) => DialogResult = true;
}
