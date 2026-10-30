using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;

namespace Polaris.Gps.Domain.Editing;

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

    /// <summary>
    /// As <see cref="CreateAddition"/> for several points drawn at once (a stretch along a path): they go in order right
    /// after <paramref name="after"/>, or at the end of <paramref name="target"/>, or in a new track.
    /// </summary>
    public static IEditCommand CreateAdditions(TrailData data, object? target, TrailPoint? after, IReadOnlyList<TrailPoint> points)
    {
        if (points.Count == 0)
        {
            throw new ArgumentException("At least one point is needed.", nameof(points));
        }

        if (after is not null && data.Locate(after) is { } location)
        {
            return new InsertPointsCommand(location.Points, location.Points.IndexOf(after) + 1, points);
        }

        switch (target)
        {
            case Track { Segments.Count: > 0 } track:
                return new InsertPointsCommand(track.Segments[^1].Points, track.Segments[^1].Points.Count, points);
            case Route route:
                return new InsertPointsCommand(route.Points, route.Points.Count, points);
            case List<TrailPoint> waypoints when ReferenceEquals(waypoints, data.Waypoints):
                return new InsertPointsCommand(waypoints, waypoints.Count, points);
            case Track track:
                var segment = new TrackSegment();
                segment.Points.AddRange(points);
                return new AddItemCommand<TrackSegment>(track.Segments, segment);
            case null:
                var created = new Track();
                created.Segments.Add(new TrackSegment());
                created.Segments[0].Points.AddRange(points);
                return new AddItemCommand<Track>(data.Tracks, created);
            default:
                throw new ArgumentException("The target must be a track, a route or the waypoint list of the document.", nameof(target));
        }
    }

    /// <summary>
    /// Ends a track or route where it started: appends a copy of its first point (or, with <paramref name="way"/>, the
    /// points of the way back computed by the router) to its last list. Null when it already ends at the start.
    /// </summary>
    public static IEditCommand? CloseLoop(object item, IReadOnlyList<TrailPoint>? way = null)
    {
        var (first, last) = Lists(item);
        if (first.Count == 0 || last.Count == 0 || (last[^1].Position == first[0].Position))
        {
            return null;
        }

        var points = way is { Count: > 0 } ? way : [Copy(first[0])];
        return new InsertPointsCommand(last, last.Count, points);
    }

    /// <summary>
    /// Adds the way back: copies of all the points but the last one, in reverse order, appended to the last list of the
    /// track or route, so it returns to the start along the same way. The copies have no time. Null with fewer than two points.
    /// </summary>
    public static IEditCommand? OutAndBack(object item)
    {
        var all = item switch
        {
            Track track => track.AllPoints.ToList(),
            Route route => route.Points,
            _ => throw new ArgumentException("Only tracks and routes.", nameof(item)),
        };
        if (all.Count < 2)
        {
            return null;
        }

        var back = all.Take(all.Count - 1).Reverse().Select(Copy).ToList();
        return new InsertPointsCommand(Lists(item).Last, Lists(item).Last.Count, back);
    }

    /// <summary>The first and the last point list of a track (first and last segment) or route.</summary>
    private static (List<TrailPoint> First, List<TrailPoint> Last) Lists(object item) => item switch
    {
        Track { Segments.Count: > 0 } track => (track.Segments[0].Points, track.Segments[^1].Points),
        Track => ([], []),
        Route route => (route.Points, route.Points),
        _ => throw new ArgumentException("Only tracks and routes.", nameof(item)),
    };

    /// <summary>A copy of a point for drawing: position and elevation (with its origin), no time, marked as added.</summary>
    private static TrailPoint Copy(TrailPoint point) => new(point.Position)
    {
        Elevation = point.Elevation,
        ElevationSource = point.ElevationSource,
        IsAdded = true,
    };

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
