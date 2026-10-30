using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;

namespace Polaris.Gps.Domain.Editing;

/// <summary>The user-editable values of a point (EDIT-04, WPE-02).</summary>
public sealed record PointValues(
    GeoPosition Position,
    double? Elevation,
    DateTimeOffset? Time,
    string? Name,
    string? Comment,
    string? Description)
{
    /// <summary>Waypoint symbol (GPX sym), e.g. "Summit"; null when there is none.</summary>
    public string? Symbol { get; init; }

    public static PointValues From(TrailPoint point) =>
        new(point.Position, point.Elevation, point.Time, point.Name, point.Comment, point.Description) { Symbol = point.Symbol };

    public void ApplyTo(TrailPoint point)
    {
        point.Position = Position;
        point.Elevation = Elevation;
        point.Time = Time;
        point.Name = Name;
        point.Comment = Comment;
        point.Description = Description;
        point.Symbol = Symbol;
    }
}
