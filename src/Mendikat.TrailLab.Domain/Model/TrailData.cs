using Mendikat.TrailLab.Domain.Geo;

namespace Mendikat.TrailLab.Domain.Model;

/// <summary>Geographic content of a document: waypoints, routes and tracks, independent of the file format.</summary>
public sealed class TrailData
{
    public List<TrailPoint> Waypoints { get; } = [];

    public List<Route> Routes { get; } = [];

    public List<Track> Tracks { get; } = [];

    /// <summary>Opaque document-level data owned by the file format (GPX header, namespaces…).</summary>
    public object? FormatState { get; set; }

    public bool IsEmpty => Waypoints.Count == 0 && Routes.Count == 0 && Tracks.All(t => t.PointCount == 0);

    public IEnumerable<GeoPosition> AllPositions =>
        Waypoints.Select(p => p.Position)
            .Concat(Routes.SelectMany(r => r.Points).Select(p => p.Position))
            .Concat(Tracks.SelectMany(t => t.AllPoints).Select(p => p.Position));

    /// <summary>Extent of all points, or null if there are none.</summary>
    public GeoBounds? GetBounds() => GeoBounds.FromPositions(AllPositions);
}
