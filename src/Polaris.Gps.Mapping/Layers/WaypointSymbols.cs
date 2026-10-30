using Mapsui.Styles;

namespace Polaris.Gps.Mapping.Layers;

/// <summary>What a waypoint symbol represents; the application names it in the user's language.</summary>
public enum WaypointKind
{
    Generic,
    Summit,
    Pass,
    Parking,
    Shelter,
    Campground,
    DrinkingWater,
    Danger,
    Viewpoint,
    Information,
    TrailHead,
}

/// <summary>A waypoint symbol: the GPX <c>sym</c> written to the file (Garmin names, read by most GPS) and its kind.</summary>
public sealed record WaypointSymbol(string GpxName, WaypointKind Kind);

/// <summary>
/// The waypoint symbols offered when editing a waypoint and their map icons: a coloured disc with a white pictogram
/// (inline SVG). Symbols read from files are matched by name, ignoring case, also with a few usual synonyms;
/// unknown or missing ones keep the plain waypoint triangle in the document colour.
/// </summary>
public static class WaypointSymbols
{
    /// <summary>The symbols, the generic one first. GPX names are the Garmin ones.</summary>
    public static IReadOnlyList<WaypointSymbol> All { get; } =
    [
        new("Waypoint", WaypointKind.Generic),
        new("Summit", WaypointKind.Summit),
        new("Pass", WaypointKind.Pass),
        new("Parking Area", WaypointKind.Parking),
        new("Lodging", WaypointKind.Shelter),
        new("Campground", WaypointKind.Campground),
        new("Drinking Water", WaypointKind.DrinkingWater),
        new("Danger Area", WaypointKind.Danger),
        new("Scenic Area", WaypointKind.Viewpoint),
        new("Information", WaypointKind.Information),
        new("Trail Head", WaypointKind.TrailHead),
    ];

    private static readonly Dictionary<string, WaypointKind> Synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Peak"] = WaypointKind.Summit,
        ["Mountain"] = WaypointKind.Summit,
        ["Parking"] = WaypointKind.Parking,
        ["Shelter"] = WaypointKind.Shelter,
        ["Hut"] = WaypointKind.Shelter,
        ["Camping"] = WaypointKind.Campground,
        ["Water"] = WaypointKind.DrinkingWater,
        ["Water Source"] = WaypointKind.DrinkingWater,
        ["Danger"] = WaypointKind.Danger,
        ["Viewpoint"] = WaypointKind.Viewpoint,
        ["Scenic"] = WaypointKind.Viewpoint,
        ["Info"] = WaypointKind.Information,
        ["Trailhead"] = WaypointKind.TrailHead,
        ["Saddle"] = WaypointKind.Pass,
        ["Mountain Pass"] = WaypointKind.Pass,
    };

    private static readonly Dictionary<WaypointKind, IStyle> Icons = new();

    /// <summary>The kind of a GPX symbol name, or <see cref="WaypointKind.Generic"/> when unknown or missing.</summary>
    public static WaypointKind KindOf(string? gpxName)
    {
        if (string.IsNullOrWhiteSpace(gpxName))
        {
            return WaypointKind.Generic;
        }

        var name = gpxName.Trim();
        return All.FirstOrDefault(s => string.Equals(s.GpxName, name, StringComparison.OrdinalIgnoreCase))?.Kind
            ?? Synonyms.GetValueOrDefault(name, WaypointKind.Generic);
    }

    /// <summary>The map icon of a kind; null for <see cref="WaypointKind.Generic"/> (the document-coloured triangle is used).</summary>
    public static IStyle? Icon(WaypointKind kind)
    {
        if (kind == WaypointKind.Generic)
        {
            return null;
        }

        lock (Icons)
        {
            if (!Icons.TryGetValue(kind, out var style))
            {
                style = new ImageStyle { Image = "svg-content://" + Svg(kind), SymbolScale = 0.9 };
                Icons[kind] = style;
            }

            return style;
        }
    }

    /// <summary>A 24 × 24 SVG: disc of the kind's colour, white outline and white pictogram.</summary>
    public static string Svg(WaypointKind kind)
    {
        var (color, pictogram) = kind switch
        {
            WaypointKind.Summit => ("#8D5A2B", "<path d='M5 17 L10.5 8 L13 12 L14.5 10 L19 17 Z' fill='white'/>"),
            WaypointKind.Pass => ("#6D4C8D", "<path d='M4 9 Q12 19 20 9 L20 16 L4 16 Z' fill='white'/>"),
            WaypointKind.Parking => ("#1F5FBF", "<path d='M9 6 H13.5 A3.5 3.5 0 0 1 13.5 13 H11.3 V18 H9 Z M11.3 8 V11 H13.3 A1.5 1.5 0 0 0 13.3 8 Z' fill='white' fill-rule='evenodd'/>"),
            WaypointKind.Shelter => ("#B23A2E", "<path d='M5 12 L12 6 L19 12 L17 12 L17 18 L7 18 L7 12 Z' fill='white'/>"),
            WaypointKind.Campground => ("#2E7D32", "<path d='M12 6 L19 18 L5 18 Z M12 12 L10 18 L14 18 Z' fill='white' fill-rule='evenodd'/>"),
            WaypointKind.DrinkingWater => ("#0E8FC0", "<path d='M12 5 C15 10 17 12.5 17 15 A5 5 0 0 1 7 15 C7 12.5 9 10 12 5 Z' fill='white'/>"),
            WaypointKind.Danger => ("#E0A800", "<path d='M11 6 H13 L12.8 14 H11.2 Z M12 15.5 A1.4 1.4 0 1 1 11.99 15.5 Z' fill='white'/>"),
            WaypointKind.Viewpoint => ("#7B3FA0", "<path d='M12 5 L13.9 9.9 L19 10.2 L15 13.5 L16.3 18.6 L12 15.7 L7.7 18.6 L9 13.5 L5 10.2 L10.1 9.9 Z' fill='white'/>"),
            WaypointKind.Information => ("#1F5FBF", "<path d='M11 10 H13 V18 H11 Z M12 6 A1.4 1.4 0 1 1 11.99 6 Z' fill='white'/>"),
            WaypointKind.TrailHead => ("#2E7D32", "<path d='M8 5 H10 V19 H8 Z M10 5 L18 8.5 L10 12 Z' fill='white'/>"),
            _ => ("#555555", string.Empty),
        };
        return "<svg xmlns='http://www.w3.org/2000/svg' width='24' height='24' viewBox='0 0 24 24'>"
            + $"<circle cx='12' cy='12' r='11' fill='{color}' stroke='white' stroke-width='1.5'/>{pictogram}</svg>";
    }
}
