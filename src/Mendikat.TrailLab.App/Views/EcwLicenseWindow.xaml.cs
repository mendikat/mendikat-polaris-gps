using System.Windows;

namespace Mendikat.TrailLab.App.Views;

/// <summary>The ERDAS ECW/JP2 end-user licence. "No acepto" (the default) closes it with ECW left disabled.</summary>
public partial class EcwLicenseWindow : Window
{
    public EcwLicenseWindow()
    {
        InitializeComponent();
    }

    private void OnAccept(object sender, RoutedEventArgs e) => DialogResult = true;
}
