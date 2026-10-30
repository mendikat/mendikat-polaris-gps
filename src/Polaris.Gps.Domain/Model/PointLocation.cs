namespace Polaris.Gps.Domain.Model;

/// <summary>Where a point lives: the ordered list that contains it and the item that owns that list.</summary>
/// <param name="Points">The list containing the point (segment points, route points or waypoints).</param>
/// <param name="Item">The <see cref="Track"/>, <see cref="Route"/> or the waypoint list itself.</param>
/// <param name="SegmentIndex">0-based segment index for tracks; null otherwise.</param>
public sealed record PointLocation(List<TrailPoint> Points, object Item, int? SegmentIndex);

public static class TrailDataExtensions
{
    /// <summary>Finds the list and item containing <paramref name="point"/> (by reference), or null.</summary>
    public static PointLocation? Locate(this TrailData data, TrailPoint point)
    {
        if (data.Waypoints.Contains(point))
        {
            return new PointLocation(data.Waypoints, data.Waypoints, null);
        }

        foreach (var route in data.Routes)
        {
            if (route.Points.Contains(point))
            {
                return new PointLocation(route.Points, route, null);
            }
        }

        foreach (var track in data.Tracks)
        {
            for (var s = 0; s < track.Segments.Count; s++)
            {
                if (track.Segments[s].Points.Contains(point))
                {
                    return new PointLocation(track.Segments[s].Points, track, s);
                }
            }
        }

        return null;
    }
}
