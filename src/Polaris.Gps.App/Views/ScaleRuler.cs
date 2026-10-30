using System.Windows;
using System.Windows.Media;

namespace Polaris.Gps.App.Views;

/// <summary>The bar of the map scale: alternating black and white blocks inside a black frame, as in CompeGPS.</summary>
public sealed class ScaleRuler : FrameworkElement
{
    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
        nameof(Segments), typeof(int), typeof(ScaleRuler), new FrameworkPropertyMetadata(5, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Pen Frame = Freeze(new Pen(Brushes.Black, 1));

    public int Segments
    {
        get => (int)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 2 || height < 2 || Segments < 1)
        {
            return;
        }

        var block = width / Segments;
        for (var i = 0; i < Segments; i++)
        {
            drawingContext.DrawRectangle(i % 2 == 0 ? Brushes.Black : Brushes.White, null, new Rect(i * block, 0, block, height));
        }

        drawingContext.DrawRectangle(null, Frame, new Rect(0.5, 0.5, width - 1, height - 1));
    }

    private static Pen Freeze(Pen pen)
    {
        pen.Freeze();
        return pen;
    }
}
