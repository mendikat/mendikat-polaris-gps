using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Polaris.Gps.App.Views;

/// <summary>
/// Small flags drawn as vectors (3:2) for the language list. Emoji flags are not used: Windows shows them as letters,
/// and the Catalan and Basque flags have no emoji.
/// </summary>
public static class LanguageFlags
{
    private const double Width = 30;
    private const double Height = 20;

    private static readonly Dictionary<string, DrawingImage> Cache = [];

    /// <summary>The flag of a language code (es, ca, eu, fr, pt), or null for any other.</summary>
    public static DrawingImage? For(string code)
    {
        if (Cache.TryGetValue(code, out var cached))
        {
            return cached;
        }

        var group = new DrawingGroup { ClipGeometry = new RectangleGeometry(new Rect(0, 0, Width, Height)) };
        switch (code)
        {
            case "es":
                Fill(group, "#AA151B", 0, 0, Width, Height);
                Fill(group, "#F1BF00", 0, 5, Width, 10);
                break;
            case "ca":
                Fill(group, "#FCDD09", 0, 0, Width, Height);
                for (var i = 1; i < 9; i += 2)
                {
                    Fill(group, "#DA121A", 0, i * Height / 9, Width, Height / 9);
                }

                break;
            case "eu":
                Fill(group, "#D52B1E", 0, 0, Width, Height);
                Line(group, "#009B48", new Point(0, 0), new Point(Width, Height), 3.2);
                Line(group, "#009B48", new Point(Width, 0), new Point(0, Height), 3.2);
                Fill(group, "#FFFFFF", (Width / 2) - 1.4, 0, 2.8, Height);
                Fill(group, "#FFFFFF", 0, (Height / 2) - 1.4, Width, 2.8);
                break;
            case "fr":
                Fill(group, "#002654", 0, 0, Width / 3, Height);
                Fill(group, "#FFFFFF", Width / 3, 0, Width / 3, Height);
                Fill(group, "#ED2939", 2 * Width / 3, 0, Width / 3, Height);
                break;
            case "pt":
                Fill(group, "#046A38", 0, 0, 12, Height);
                Fill(group, "#DA291C", 12, 0, Width - 12, Height);
                group.Children.Add(new GeometryDrawing(null, new Pen(Brush("#FFE900"), 1.4), new EllipseGeometry(new Point(12, 10), 4.6, 4.6)));
                Fill(group, "#FFFFFF", 10.2, 7.6, 3.6, 4.6);
                Fill(group, "#DA291C", 10.9, 8.3, 2.2, 3.2);
                break;
            default:
                return null;
        }

        // Thin edge, so the white parts do not vanish against the window.
        group.Children.Add(new GeometryDrawing(null, new Pen(Brush("#80000000"), 0.6), new RectangleGeometry(new Rect(0.3, 0.3, Width - 0.6, Height - 0.6))));
        var image = new DrawingImage(group);
        image.Freeze();
        Cache[code] = image;
        return image;
    }

    private static void Fill(DrawingGroup group, string color, double x, double y, double width, double height) =>
        group.Children.Add(new GeometryDrawing(Brush(color), null, new RectangleGeometry(new Rect(x, y, width, height))));

    private static void Line(DrawingGroup group, string color, Point from, Point to, double thickness) =>
        group.Children.Add(new GeometryDrawing(null, new Pen(Brush(color), thickness), new LineGeometry(from, to)));

    private static SolidColorBrush Brush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}

/// <summary>Language code → its flag, for bindings.</summary>
public sealed class LanguageFlagConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string code ? LanguageFlags.For(code) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
