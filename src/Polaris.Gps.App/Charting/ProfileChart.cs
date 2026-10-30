using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Polaris.Gps.App.Resources;
using Polaris.Gps.Domain.Profiles;

namespace Polaris.Gps.App.Charting;

/// <summary>
/// Draws an elevation profile: distance (km) on X, elevation (m) on Y (PROF-01). Missing elevations and segment
/// boundaries leave gaps (PROF-03). Shows the selected position and the pointer position, and reports the
/// distance under the pointer and clicks (PROF-04). Own WPF drawing: no charting library is needed (ADR-0009).
/// </summary>
public sealed class ProfileChart : FrameworkElement
{
    public static readonly DependencyProperty ProfileProperty = DependencyProperty.Register(
        nameof(Profile), typeof(ElevationProfile), typeof(ProfileChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CursorDistanceProperty = DependencyProperty.Register(
        nameof(CursorDistance), typeof(double?), typeof(ProfileChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HoverDistanceProperty = DependencyProperty.Register(
        nameof(HoverDistance), typeof(double?), typeof(ProfileChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    // Theme colours, bound to the Fluent theme brushes in ProfileWindow.xaml so the chart follows light or dark mode.
    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(
        nameof(Background), typeof(Brush), typeof(ProfileChart), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(Brush), typeof(ProfileChart), new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SecondaryForegroundProperty = DependencyProperty.Register(
        nameof(SecondaryForeground), typeof(Brush), typeof(ProfileChart), new FrameworkPropertyMetadata(Brushes.DimGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GridBrushProperty = DependencyProperty.Register(
        nameof(GridBrush), typeof(Brush), typeof(ProfileChart),
        new FrameworkPropertyMetadata(Frozen(new SolidColorBrush(Color.FromRgb(225, 225, 225))), FrameworkPropertyMetadataOptions.AffectsRender));

    private const double LeftMargin = 56;
    private const double RightMargin = 16;
    // Room above the plot for the elevation axis title, so it never touches the top tick label.
    private const double TopMargin = 30;
    private const double BottomMargin = 40;

    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private static readonly Typeface Font = new("Segoe UI");
    private static readonly Pen LinePen = Frozen(new Pen(new SolidColorBrush(Color.FromRgb(200, 30, 50)), 2) { LineJoin = PenLineJoin.Round });
    private static readonly Brush AreaBrush = Frozen(new SolidColorBrush(Color.FromArgb(60, 200, 30, 50)));
    private static readonly Pen HoverPen = Frozen(new Pen(new SolidColorBrush(Color.FromRgb(25, 90, 220)), 1) { DashStyle = DashStyles.Dash });
    private static readonly Brush CursorFill = Frozen(new SolidColorBrush(Color.FromRgb(255, 230, 0)));

    public ProfileChart()
    {
        Focusable = true;
        Cursor = Cursors.Cross;
    }

    /// <summary>Distance in meters under the pointer, or null when it leaves the chart.</summary>
    public event EventHandler<double?>? Hovered;

    /// <summary>Distance in meters where the user clicked.</summary>
    public event EventHandler<double>? Clicked;

    public ElevationProfile? Profile
    {
        get => (ElevationProfile?)GetValue(ProfileProperty);
        set => SetValue(ProfileProperty, value);
    }

    /// <summary>Distance of the selected point (solid line), in meters.</summary>
    public double? CursorDistance
    {
        get => (double?)GetValue(CursorDistanceProperty);
        set => SetValue(CursorDistanceProperty, value);
    }

    /// <summary>Distance under the pointer (dashed line), in meters.</summary>
    public double? HoverDistance
    {
        get => (double?)GetValue(HoverDistanceProperty);
        set => SetValue(HoverDistanceProperty, value);
    }

    /// <summary>Chart background.</summary>
    public Brush Background
    {
        get => (Brush)GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>Axis titles and the selected position.</summary>
    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Tick labels, plot frame and messages.</summary>
    public Brush SecondaryForeground
    {
        get => (Brush)GetValue(SecondaryForegroundProperty);
        set => SetValue(SecondaryForegroundProperty, value);
    }

    /// <summary>Grid lines.</summary>
    public Brush GridBrush
    {
        get => (Brush)GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    private Rect Plot => new(LeftMargin, TopMargin, Math.Max(1, ActualWidth - LeftMargin - RightMargin), Math.Max(1, ActualHeight - TopMargin - BottomMargin));

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Background, null, new Rect(0, 0, ActualWidth, ActualHeight));
        var profile = Profile;
        if (profile is null || profile.Samples.Count == 0 || !profile.HasElevation)
        {
            var message = profile is null || profile.Samples.Count == 0 ? Strings.ProfileNoPoints : Strings.ProfileNoElevation;
            DrawText(dc, message, new Point(ActualWidth / 2, ActualHeight / 2), 14, SecondaryForeground, HorizontalAlignment.Center);
            return;
        }

        var plot = Plot;
        var (minY, maxY) = ElevationRange(profile);
        var maxX = Math.Max(profile.TotalDistance, 1);
        Point ToScreen(double distance, double elevation) => new(
            plot.Left + distance / maxX * plot.Width,
            plot.Bottom - (elevation - minY) / (maxY - minY) * plot.Height);

        DrawGrid(dc, plot, maxX, minY, maxY);

        foreach (var run in profile.ElevationRuns())
        {
            var points = run.Select(s => ToScreen(s.Distance, s.Elevation!.Value)).ToList();
            var area = new StreamGeometry();
            using (var ctx = area.Open())
            {
                ctx.BeginFigure(new Point(points[0].X, plot.Bottom), isFilled: true, isClosed: true);
                ctx.PolyLineTo(points, isStroked: false, isSmoothJoin: false);
                ctx.LineTo(new Point(points[^1].X, plot.Bottom), isStroked: false, isSmoothJoin: false);
            }

            area.Freeze();
            dc.DrawGeometry(AreaBrush, null, area);

            var line = new StreamGeometry();
            using (var ctx = line.Open())
            {
                ctx.BeginFigure(points[0], isFilled: false, isClosed: false);
                ctx.PolyLineTo(points, isStroked: true, isSmoothJoin: true);
            }

            line.Freeze();
            dc.DrawGeometry(null, LinePen, line);
        }

        if (HoverDistance is { } hover)
        {
            var x = plot.Left + Math.Clamp(hover, 0, maxX) / maxX * plot.Width;
            dc.DrawLine(HoverPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
        }

        if (CursorDistance is { } cursor && profile.Nearest(cursor) is { } selected)
        {
            var x = plot.Left + selected.Distance / maxX * plot.Width;
            var cursorPen = Frozen(new Pen(Foreground, 1.5));
            dc.DrawLine(cursorPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            if (selected.Elevation is { } elevation)
            {
                dc.DrawEllipse(CursorFill, cursorPen, ToScreen(selected.Distance, elevation), 5, 5);
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Hovered?.Invoke(this, DistanceAt(e.GetPosition(this)));
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        Hovered?.Invoke(this, null);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        if (DistanceAt(e.GetPosition(this)) is { } distance)
        {
            Clicked?.Invoke(this, distance);
        }
    }

    private double? DistanceAt(Point position)
    {
        var plot = Plot;
        if (Profile is not { Samples.Count: > 0 } profile || position.X < plot.Left - 4 || position.X > plot.Right + 4)
        {
            return null;
        }

        return Math.Clamp((position.X - plot.Left) / plot.Width, 0, 1) * Math.Max(profile.TotalDistance, 1);
    }

    private static (double Min, double Max) ElevationRange(ElevationProfile profile)
    {
        var min = profile.MinElevation!.Value;
        var max = profile.MaxElevation!.Value;
        var padding = Math.Max(10, (max - min) * 0.05);
        return (min - padding, max + padding);
    }

    private void DrawGrid(DrawingContext dc, Rect plot, double maxX, double minY, double maxY)
    {
        var gridPen = Frozen(new Pen(GridBrush, 1));
        foreach (var km in AxisTicks.Ticks(0, maxX / 1000, Math.Max(2, (int)(plot.Width / 80))))
        {
            var x = plot.Left + km * 1000 / maxX * plot.Width;
            dc.DrawLine(gridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            DrawText(dc, km.ToString("0.##", Spanish), new Point(x, plot.Bottom + 4), 11, SecondaryForeground, HorizontalAlignment.Center);
        }

        foreach (var meters in AxisTicks.Ticks(minY, maxY, Math.Max(2, (int)(plot.Height / 40))))
        {
            var y = plot.Bottom - (meters - minY) / (maxY - minY) * plot.Height;
            dc.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            DrawText(dc, meters.ToString("N0", Spanish), new Point(plot.Left - 6, y - 8), 11, SecondaryForeground, HorizontalAlignment.Right);
        }

        dc.DrawRectangle(null, Frozen(new Pen(SecondaryForeground, 1)), plot);
        DrawText(dc, Strings.ProfileAxisDistance, new Point(plot.Left + plot.Width / 2, plot.Bottom + 20), 11, Foreground, HorizontalAlignment.Center);
        DrawText(dc, Strings.ProfileAxisElevation, new Point(4, 2), 11, Foreground, HorizontalAlignment.Left);
    }

    private void DrawText(DrawingContext dc, string text, Point anchor, double size, Brush brush, HorizontalAlignment alignment)
    {
        var formatted = new FormattedText(text, Spanish, FlowDirection.LeftToRight, Font, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var x = alignment switch
        {
            HorizontalAlignment.Center => anchor.X - formatted.Width / 2,
            HorizontalAlignment.Right => anchor.X - formatted.Width,
            _ => anchor.X,
        };
        dc.DrawText(formatted, new Point(x, anchor.Y));
    }

    private static T Frozen<T>(T freezable)
        where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
