using Mendikat.TrailLab.Domain.Geo;

namespace Mendikat.TrailLab.Domain.Model;

/// <summary>
/// A point of a track, route or waypoint list. Optional values are null when absent, never 0 (GPX-05).
/// </summary>
public sealed class TrailPoint
{
    public TrailPoint(GeoPosition position)
    {
        Position = position;
    }

    public GeoPosition Position { get; set; }

    /// <summary>Elevation in meters, as stored in the source (no vertical datum conversion).</summary>
    public double? Elevation { get; set; }

    /// <summary>Timestamp as an instant. Formats without a time zone are read as UTC (see load warnings).</summary>
    public DateTimeOffset? Time { get; set; }

    public string? Name { get; set; }

    public string? Comment { get; set; }

    public string? Description { get; set; }

    public string? Symbol { get; set; }

    public string? Type { get; set; }

    /// <summary>Source elements without a typed property (GPX: fix, sat, hdop, link, extensions…), kept for saving.</summary>
    public List<XmlFragment> OtherElements { get; } = [];

    /// <summary>Opaque data owned by the file format (e.g. the original WPT line), used to save unedited fields verbatim.</summary>
    public object? FormatState { get; set; }

    /// <summary>True for points inserted by the user during this session (drawn with a distinct symbol).</summary>
    public bool IsAdded { get; set; }
}
