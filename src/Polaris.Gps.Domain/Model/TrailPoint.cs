using Polaris.Gps.Domain.Geo;

namespace Polaris.Gps.Domain.Model;

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

    /// <summary>True for points inserted by the user during this session (drawn with a distinct symbol).</summary>
    public bool IsAdded { get; set; }

    /// <summary>Where the current elevation comes from, for this session only (DEM-06). Not written to files.</summary>
    public ElevationSource ElevationSource { get; set; }
}

/// <summary>Origin of a point's elevation during the session (DEM-06).</summary>
public enum ElevationSource
{
    /// <summary>As read from the file (or none).</summary>
    Original,

    /// <summary>Interpolated from its neighbours when the point was inserted.</summary>
    Interpolated,

    /// <summary>Taken from a digital elevation model.</summary>
    Dem,

    /// <summary>Typed by the user.</summary>
    Edited,
}
