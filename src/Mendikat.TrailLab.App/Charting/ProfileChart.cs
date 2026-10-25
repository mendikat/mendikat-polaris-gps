using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Mendikat.TrailLab.App.Resources;
using Mendikat.TrailLab.Domain.Profiles;

namespace Mendikat.TrailLab.App.Charting;

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

    private const double LeftMargin = 56;
    private const double RightMargin = 16;
    // Room above the plot for the elevation axis title, so it never touches the top tick label.
    private const double TopMargin = 30;
    private const double BottomMargin = 40;

    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private static readonly Typeface Font = new("Segoe UI");
    private static readonly Pen GridPen = Frozen(new Pen(new SolidColorBrush(Color.FromRgb(225, 225, 225)), 1));
    private static readonly Pen AxisPen = Frozen(new Pen(Brushes.Gray, 1));
    private static readonly Pen LinePen = Frozen(new Pen(new SolidColorBrush(Color.FromRgb(200, 30, 50)), 2) { LineJoin = PenLineJoin.Round });
    private static readonly Brush AreaBrush = Frozen(new SolidColorBrush(Color.FromArgb(60, 200, 30, 50)));
    private static readonly Pen CursorPen = Frozen(new Pen(Brushes.Black, 1.5));
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

    private Rect Plot => new(LeftMargin, TopMargin, Math.Max(1, ActualWidth - LeftMargin - RightMargin), Math.Max(1, ActualHeight - TopMargin - BottomMargin));

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, ActualWidth, ActualHeight));
        var profile = Profile;
        if (profile is null || profile.Samples.Count == 0 || !profile.HasElevation)
        {
            var message = profile is null || profile.Samples.Count == 0 ? Strings.ProfileNoPoints : Strings.ProfileNoElevation;
            DrawText(dc, message, new Point(ActualWidth / 2, ActualHeight / 2), 14, Brushes.Gray, HorizontalAlignment.Center);
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
            dc.DrawLine(CursorPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            if (selected.Elevation is { } elevation)
            {
                dc.DrawEllipse(CursorFill, CursorPen, ToScreen(selected.Distance, elevation), 5, 5);
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
        foreach (var km in AxisTicks.Ticks(0, maxX / 1000, Math.Max(2, (int)(plot.Width / 80))))
        {
            var x = plot.Left + km * 1000 / maxX * plot.Width;
            dc.DrawLine(GridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            DrawText(dc, km.ToString("0.##", Spanish), new Point(x, plot.Bottom + 4), 11, Brushes.DimGray, HorizontalAlignment.Center);
        }

        foreach (var meters in AxisTicks.Ticks(minY, maxY, Math.Max(2, (int)(plot.Height / 40))))
        {
            var y = plot.Bottom - (meters - minY) / (maxY - minY) * plot.Height;
            dc.DrawLine(GridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            DrawText(dc, meters.ToString("N0", Spanish), new Point(plot.Left - 6, y - 8), 11, Brushes.DimGray, HorizontalAlignment.Right);
        }

        dc.DrawRectangle(null, AxisPen, plot);
        DrawText(dc, Strings.ProfileAxisDistance, new Point(plot.Left + plot.Width / 2, plot.Bottom + 20), 11, Brushes.Black, HorizontalAlignment.Center);
        DrawText(dc, Strings.ProfileAxisElevation, new Point(4, 2), 11, Brushes.Black, HorizontalAlignment.Left);
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
