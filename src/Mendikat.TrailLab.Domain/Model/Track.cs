namespace Mendikat.TrailLab.Domain.Model;

/// <summary>Ordered sequence of track points (GPX trkseg). Segments are never merged implicitly.</summary>
public sealed class TrackSegment
{
    public List<TrailPoint> Points { get; } = [];

    public List<XmlFragment> OtherElements { get; } = [];
}

/// <summary>A recorded track (GPX trk) made of segments.</summary>
public sealed class Track
{
    public string? Name { get; set; }

    public string? Comment { get; set; }

    public string? Description { get; set; }

    public string? Type { get; set; }

    public List<TrackSegment> Segments { get; } = [];

    public List<XmlFragment> OtherElements { get; } = [];

    public int PointCount => Segments.Sum(s => s.Points.Count);

    public IEnumerable<TrailPoint> AllPoints => Segments.SelectMany(s => s.Points);
}

/// <summary>A planned route (GPX rte): ordered route points.</summary>
public sealed class Route
{
    public string? Name { get; set; }

    public string? Comment { get; set; }

    public string? Description { get; set; }

    public string? Type { get; set; }

    public List<TrailPoint> Points { get; } = [];

    public List<XmlFragment> OtherElements { get; } = [];
}
