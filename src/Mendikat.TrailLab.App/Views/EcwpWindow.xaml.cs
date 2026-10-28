using System.Windows;
using System.Windows.Controls;
using Mendikat.TrailLab.Mapping.Local;

namespace Mendikat.TrailLab.App.Views;

/// <summary>Asks for the address of an ECWP stream (File › Open online cartography). Only ecwp:// and ecwps:// are accepted.</summary>
public partial class EcwpWindow : Window
{
    public EcwpWindow()
    {
        InitializeComponent();
    }

    public string? Url { get; private set; }

    private void OnUrlChanged(object sender, TextChangedEventArgs e) =>
        OpenButton.IsEnabled = LocalMapLoader.IsEcwpUrl(UrlBox.Text.Trim()) && UrlBox.Text.Trim().Length > "ecwp://".Length;

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        Url = UrlBox.Text.Trim();
        DialogResult = true;
    }
}
