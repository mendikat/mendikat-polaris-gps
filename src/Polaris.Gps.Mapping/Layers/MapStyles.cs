using Mapsui.Styles;

namespace Polaris.Gps.Mapping.Layers;

/// <summary>
/// All map symbols in one place (LAY-02): track line, track point, selected point, waypoint, added point.
/// Each document gets a color from <see cref="DocumentColors"/> so several tracks can be told apart.
/// </summary>
public static class MapStyles
{
    /// <summary>Resolution (meters per pixel) below which individual track points and labels are drawn.</summary>
    public const double PointsMaxResolution = 10;

    public static IReadOnlyList<Color> DocumentColors { get; } =
    [
        new(220, 20, 60),   // crimson
        new(25, 90, 220),   // blue
        new(230, 120, 0),   // orange
        new(140, 40, 170),  // purple
        new(0, 140, 70),    // green
        new(200, 0, 160),   // magenta
    ];

    public static Color ColorFor(int documentIndex) => DocumentColors[Math.Abs(documentIndex) % DocumentColors.Count];

    public static IStyle TrackLine(Color color) => new VectorStyle { Line = new Pen(color, 3) };

    /// <summary>A stretch of a track coloured by elevation, slope or speed: a little wider than the plain line.</summary>
    public static IStyle ColoredTrackLine(Color color) => new VectorStyle { Line = new Pen(color, 4) };

    public static IStyle RouteLine(Color color) =>
        new VectorStyle { Line = new Pen(color, 2.5) { PenStyle = PenStyle.Dash } };

    public static IStyle TrackPoint(Color color) => new SymbolStyle
    {
        SymbolType = SymbolType.Ellipse,
        SymbolScale = 0.25,
        Fill = new Brush(Color.White),
        Outline = new Pen(color, 1.5),
        MaxVisible = PointsMaxResolution,
    };

    /// <summary>Point added by the user (EDIT-03): green square, always visible.</summary>
    public static IStyle AddedPoint { get; } = new SymbolStyle
    {
        SymbolType = SymbolType.Rectangle,
        SymbolScale = 0.35,
        Fill = new Brush(new Color(40, 200, 40)),
        Outline = new Pen(Color.Black, 1),
    };

    /// <summary>
    /// Selected point (LAY-03): a yellow ring with a black edge, always visible. It is hollow and wider than the waypoint
    /// icons, so the symbol of a selected waypoint stays visible inside it.
    /// </summary>
    public static IStyle SelectedPoint { get; } = new SymbolStyle
    {
        SymbolType = SymbolType.Ellipse,
        SymbolScale = 0.95,
        Fill = new Brush(Color.Transparent),
        Outline = new Pen(new Color(255, 215, 0), 4),
    };

    /// <summary>Black edge drawn under <see cref="SelectedPoint"/>, so the ring reads on light and dark maps.</summary>
    public static IStyle SelectedPointEdge { get; } = new SymbolStyle
    {
        SymbolType = SymbolType.Ellipse,
        SymbolScale = 0.95,
        Fill = new Brush(Color.Transparent),
        Outline = new Pen(Color.Black, 6.5),
    };

    /// <summary>Point under the pointer in the elevation profile (PROF-04): blue ring, always visible.</summary>
    public static IStyle HoverPoint { get; } = new SymbolStyle
    {
        SymbolType = SymbolType.Ellipse,
        SymbolScale = 0.7,
        Fill = new Brush(Color.Transparent),
        Outline = new Pen(new Color(25, 90, 220), 3),
    };

    public static IStyle Waypoint(Color color) => new SymbolStyle
    {
        SymbolType = SymbolType.Triangle,
        SymbolScale = 0.55,
        Fill = new Brush(color),
        Outline = new Pen(Color.White, 1.5),
    };

    /// <param name="maxResolution">Coarsest resolution at which it is drawn: lower where it would overlap another label.</param>
    /// <summary>UTM grid line: thin, dark and translucent, so it reads on any cartography without hiding it.</summary>
    public static IStyle GridLine { get; } = new VectorStyle { Line = new Pen(new Color(20, 20, 60, 110), 1) };

    /// <summary>Label of a grid line, in kilometres or metres; easting labels sit above the bottom, northing ones right of the left edge.</summary>
    public static IStyle GridLabel(string text, bool easting) => new LabelStyle
    {
        Text = text,
        Offset = easting ? new Offset(0, -10) : new Offset(18, 0),
        BackColor = new Brush(new Color(255, 255, 255, 190)),
        ForeColor = new Color(20, 20, 60),
        Font = new Font { Size = 10 },
    };

    public static IStyle WaypointLabel(string text, double maxResolution = PointsMaxResolution * 10) => new LabelStyle
    {
        Text = text,
        Offset = new Offset(0, -18),
        BackColor = new Brush(new Color(255, 255, 255, 200)),
        ForeColor = Color.Black,
        MaxVisible = maxResolution,
    };
}
