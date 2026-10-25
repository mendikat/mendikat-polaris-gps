using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Domain.Model;

namespace Mendikat.TrailLab.Domain.Editing;

/// <summary>Rules for inserting points (EDIT-03, ADR-0008).</summary>
public static class PointEditing
{
    /// <summary>
    /// Builds the insertion for the Insert key: the geodesic midpoint between <paramref name="selected"/> and the next
    /// point of the same list; if it is the last point, between the previous one and it. Elevation and time are
    /// interpolated only if both neighbours have them. Returns null if the list has fewer than two points.
    /// </summary>
    public static InsertPointCommand? CreateInsertion(List<TrailPoint> owner, TrailPoint selected)
    {
        var index = owner.IndexOf(selected);
        if (index < 0)
        {
            throw new ArgumentException("The point does not belong to the list.", nameof(selected));
        }

        if (owner.Count < 2)
        {
            return null;
        }

        var (first, second) = index < owner.Count - 1 ? (index, index + 1) : (index - 1, index);
        var a = owner[first];
        var b = owner[second];
        var point = new TrailPoint(Geodesy.Midpoint(a.Position, b.Position))
        {
            Elevation = a.Elevation is { } ea && b.Elevation is { } eb ? (ea + eb) / 2 : null,
            ElevationSource = a.Elevation is not null && b.Elevation is not null ? ElevationSource.Interpolated : ElevationSource.Original,
            Time = a.Time is { } ta && b.Time is { } tb ? ta + (tb - ta) / 2 : null,
            IsAdded = true,
        };
        return new InsertPointCommand(owner, second, point);
    }

    /// <summary>
    /// Builds the addition of a point drawn by the user at <paramref name="position"/> (APP-08). It goes right after
    /// <paramref name="after"/> when given (so consecutive clicks extend the line from the last added point); otherwise
    /// at the end of <paramref name="target"/>: the last segment of a <see cref="Track"/>, a <see cref="Route"/> or the
    /// waypoint list. With no target, a new track is created for it. The point has no elevation or time.
    /// </summary>
    public static (IEditCommand Command, TrailPoint Point) CreateAddition(TrailData data, object? target, TrailPoint? after, GeoPosition position)
    {
        var point = new TrailPoint(position) { IsAdded = true };
        if (after is not null && data.Locate(after) is { } location)
        {
            return (new InsertPointCommand(location.Points, location.Points.IndexOf(after) + 1, point), point);
        }

        IEditCommand command = target switch
        {
            Track { Segments.Count: > 0 } track => Append(track.Segments[^1].Points, point),
            Track track => new AddItemCommand<TrackSegment>(track.Segments, NewSegment(point)),
            Route route => Append(route.Points, point),
            List<TrailPoint> waypoints when ReferenceEquals(waypoints, data.Waypoints) => Append(waypoints, point),
            null => new AddItemCommand<Track>(data.Tracks, NewTrack(point)),
            _ => throw new ArgumentException("The target must be a track, a route or the waypoint list of the document.", nameof(target)),
        };
        return (command, point);
    }

    private static InsertPointCommand Append(List<TrailPoint> owner, TrailPoint point) => new(owner, owner.Count, point);

    private static TrackSegment NewSegment(TrailPoint point)
    {
        var segment = new TrackSegment();
        segment.Points.Add(point);
        return segment;
    }

    private static Track NewTrack(TrailPoint point)
    {
        var track = new Track();
        track.Segments.Add(NewSegment(point));
        return track;
    }
}
