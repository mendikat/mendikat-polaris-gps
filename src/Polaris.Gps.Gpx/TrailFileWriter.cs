using Polaris.Gps.Domain.Documents;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;

namespace Polaris.Gps.Gpx;

/// <summary>Serializes a document in its own format (SAVE-01) and verifies the result before it replaces any file (SAVE-03).</summary>
public static class TrailFileWriter
{
    /// <summary>Issues to show the user before saving (GPX-04, WPE-04).</summary>
    public static IReadOnlyList<SaveIssue> Check(TrailData data, DocumentFormat format) => format switch
    {
        DocumentFormat.Gpx => GpxWriter.Check(data),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    public static byte[] Serialize(TrailData data, DocumentFormat format) => format switch
    {
        DocumentFormat.Gpx => new GpxWriter().Write(data),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    /// <summary>Copy of the document structure and positions, taken when saving starts (the model may change later).</summary>
    public static TrailShape Shape(TrailData data) => new(
        Positions(data.Waypoints),
        data.Routes.Select(r => Positions(r.Points)).ToList(),
        data.Tracks.Select(t => (IReadOnlyList<IReadOnlyList<GeoPosition>>)t.Segments.Select(s => Positions(s.Points)).ToList()).ToList());

    /// <summary>
    /// Reads the serialized bytes back and checks that the structure and every position match the expected shape.
    /// Guards against writer bugs before an existing file is replaced.
    /// </summary>
    public static bool Verify(byte[] bytes, DocumentFormat format, TrailShape expected)
    {
        TrailShape actual;
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            actual = format == DocumentFormat.Gpx
                ? Shape(new GpxReader().Read(stream).Data)
                : throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
        catch (DocumentLoadException)
        {
            return false;
        }

        return Same(expected.Waypoints, actual.Waypoints)
            && expected.Routes.Count == actual.Routes.Count
            && expected.Routes.Zip(actual.Routes).All(p => Same(p.First, p.Second))
            && expected.Tracks.Count == actual.Tracks.Count
            && expected.Tracks.Zip(actual.Tracks).All(t =>
                t.First.Count == t.Second.Count && t.First.Zip(t.Second).All(s => Same(s.First, s.Second)));
    }

    private static List<GeoPosition> Positions(List<TrailPoint> points) => points.Select(p => p.Position).ToList();

    /// <summary>Same count and positions within 1e-6° (~10 cm).</summary>
    private static bool Same(IReadOnlyList<GeoPosition> expected, IReadOnlyList<GeoPosition> actual) =>
        expected.Count == actual.Count
        && expected.Zip(actual).All(p =>
            Math.Abs(p.First.Latitude - p.Second.Latitude) < 1e-6
            && Math.Abs(p.First.Longitude - p.Second.Longitude) < 1e-6);
}

/// <summary>Positions of a document grouped as in the file: waypoints, routes, and track segments.</summary>
public sealed record TrailShape(
    IReadOnlyList<GeoPosition> Waypoints,
    IReadOnlyList<IReadOnlyList<GeoPosition>> Routes,
    IReadOnlyList<IReadOnlyList<IReadOnlyList<GeoPosition>>> Tracks);
