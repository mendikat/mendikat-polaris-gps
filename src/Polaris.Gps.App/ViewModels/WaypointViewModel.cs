using Polaris.Gps.App.Resources;
using Polaris.Gps.App.Services;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Mapping.Coordinates;
using Polaris.Gps.Mapping.Layers;

namespace Polaris.Gps.App.ViewModels;

/// <summary>A waypoint symbol in the list: the GPX name written to the file and its name in the user's language.</summary>
public sealed record WaypointSymbolOption(string? GpxName, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Dialog to add or edit a waypoint: name, comment, description and symbol. The position is shown, not edited.</summary>
public sealed class WaypointViewModel : ObservableObject
{
    private string _name;
    private string _comment;
    private string _description;
    private WaypointSymbolOption _symbol;

    /// <param name="symbol">The current GPX symbol; one not in the list is kept as an extra entry, so editing never loses it.</param>
    public WaypointViewModel(
        string title, GeoPosition position, string? name, string? comment, string? description,
        CoordinateDatum datum = CoordinateDatum.Wgs84, string? symbol = null)
    {
        Title = title;
        Position = Format(position, datum);
        _name = name ?? string.Empty;
        _comment = comment ?? string.Empty;
        _description = description ?? string.Empty;
        (Symbols, _symbol) = SymbolOptions(symbol);
    }

    public string Title { get; }

    public string Position { get; }

    public string Name { get => _name; set => SetProperty(ref _name, value); }

    public string Comment { get => _comment; set => SetProperty(ref _comment, value); }

    public string Description { get => _description; set => SetProperty(ref _description, value); }

    /// <summary>"(ninguno)", then the symbols of <see cref="WaypointSymbols.All"/>, and the file's own symbol if it is another.</summary>
    public IReadOnlyList<WaypointSymbolOption> Symbols { get; }

    public WaypointSymbolOption Symbol { get => _symbol; set => SetProperty(ref _symbol, value ?? Symbols[0]); }

    /// <summary>Trimmed text, or null when empty (the GPX element is then not written).</summary>
    public static string? Clean(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>
    /// "(ninguno)", the symbols of <see cref="WaypointSymbols.All"/> and, if <paramref name="symbol"/> is another one, an
    /// entry for it (so editing never loses it); and the entry of <paramref name="symbol"/>.
    /// </summary>
    public static (IReadOnlyList<WaypointSymbolOption> Options, WaypointSymbolOption Current) SymbolOptions(string? symbol)
    {
        var options = new List<WaypointSymbolOption> { new(null, Strings.SymbolNone) };
        options.AddRange(WaypointSymbols.All.Select(s => new WaypointSymbolOption(s.GpxName, KindName(s.Kind))));
        var current = options.FirstOrDefault(o => o.GpxName is not null && string.Equals(o.GpxName, symbol?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (current is null && !string.IsNullOrWhiteSpace(symbol))
        {
            current = new WaypointSymbolOption(symbol, symbol);
            options.Add(current);
        }

        return (options, current ?? options[0]);
    }

    public static string KindName(WaypointKind kind) => kind switch
    {
        WaypointKind.Summit => Strings.SymbolSummit,
        WaypointKind.Pass => Strings.SymbolPass,
        WaypointKind.Parking => Strings.SymbolParking,
        WaypointKind.Shelter => Strings.SymbolShelter,
        WaypointKind.Campground => Strings.SymbolCampground,
        WaypointKind.DrinkingWater => Strings.SymbolWater,
        WaypointKind.Danger => Strings.SymbolDanger,
        WaypointKind.Viewpoint => Strings.SymbolViewpoint,
        WaypointKind.Information => Strings.SymbolInformation,
        WaypointKind.TrailHead => Strings.SymbolTrailHead,
        _ => Strings.SymbolGeneric,
    };

    private static string Format(GeoPosition position, CoordinateDatum datum)
    {
        var (latitude, longitude) = PointInput.FormatPosition(position, datum);
        return $"{latitude}, {longitude}";
    }
}
