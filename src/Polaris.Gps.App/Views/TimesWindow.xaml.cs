using System.Windows;
using Polaris.Gps.App.ViewModels;

namespace Polaris.Gps.App.Views;

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
