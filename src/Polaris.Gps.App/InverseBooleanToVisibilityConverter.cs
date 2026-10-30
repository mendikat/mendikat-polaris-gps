using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Polaris.Gps.App;

/// <summary>Visible when the bound value is false (e.g. the "no documents" hint).</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
