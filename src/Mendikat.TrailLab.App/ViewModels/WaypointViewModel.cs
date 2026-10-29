using Mendikat.TrailLab.App.Services;
using Mendikat.TrailLab.Domain.Geo;

namespace Mendikat.TrailLab.App.ViewModels;

/// <summary>Dialog to add or edit a waypoint: name, comment and description. The position is shown, not edited.</summary>
public sealed class WaypointViewModel(string title, GeoPosition position, string? name, string? comment, string? description)
    : ObservableObject
{
    private string _name = name ?? string.Empty;
    private string _comment = comment ?? string.Empty;
    private string _description = description ?? string.Empty;

    public string Title { get; } = title;

    public string Position { get; } =
        $"{PointInput.FormatCoordinate(position.Latitude)}, {PointInput.FormatCoordinate(position.Longitude)}";

    public string Name { get => _name; set => SetProperty(ref _name, value); }

    public string Comment { get => _comment; set => SetProperty(ref _comment, value); }

    public string Description { get => _description; set => SetProperty(ref _description, value); }

    /// <summary>Trimmed text, or null when empty (the GPX element is then not written).</summary>
    public static string? Clean(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
