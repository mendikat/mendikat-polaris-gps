using Polaris.Gps.Domain.Editing;
using Polaris.Gps.Domain.Model;

namespace Polaris.Gps.Domain.Processing;

/// <summary>
/// Cuts of a track or route at one of its points, each as one undoable step:
/// <list type="bullet">
/// <item><see cref="Split(TrailData, Track, TrailPoint)"/>: two items; the first ends at the point, the second starts at
/// a copy of it (so neither loses the stretch through the point) and is placed right after the first.</item>
/// <item><see cref="RemoveBefore"/> / <see cref="RemoveAfter"/>: trims the item so it starts or ends at the point.</item>
/// <item><see cref="SegmentsToTracks"/>: one track per segment.</item>
/// </list>
/// Points keep all their fields; the new items copy the name (with " (2)", " (3)"…), comment, description and type.
/// </summary>
public static class TrackCutter
{
    /// <summary>
    /// Splits <paramref name="track"/> at <paramref name="point"/>, or returns null when the point is its first or last
    /// point (there would be nothing on one side). When the point ends a segment, the second track starts with the next
    /// segment and no point is copied.
    /// </summary>
    public static (IEditCommand Command, Track Second)? Split(TrailData data, Track track, TrailPoint point)
    {
        var (segmentIndex, pointIndex) = Find(track, point);
        var segment = track.Segments[segmentIndex].Points;
        var isLastOfSegment = pointIndex == segment.Count - 1;
        if ((segmentIndex == 0 && pointIndex == 0) || (segmentIndex == track.Segments.Count - 1 && isLastOfSegment))
        {
            return null;
        }

        var second = CopyHeader(track, 2);
        if (!isLastOfSegment)
        {
            var first = new TrackSegment();
            first.Points.Add(Copy(point));
            first.Points.AddRange(segment.Skip(pointIndex + 1));
            second.Segments.Add(first);
        }

        second.Segments.AddRange(track.Segments.Skip(segmentIndex + 1));
        var commands = new List<IEditCommand>
        {
            new ReplaceItemsCommand<TrailPoint>(segment, segment.Take(pointIndex + 1).ToList()),
            new ReplaceItemsCommand<TrackSegment>(track.Segments, track.Segments.Take(segmentIndex + 1).ToList()),
            new InsertItemCommand<Track>(data.Tracks, data.Tracks.IndexOf(track) + 1, second),
        };
        return (new CompositeCommand(commands), second);
    }

    /// <summary>Splits <paramref name="route"/> at <paramref name="point"/>; null at its first or last point.</summary>
    public static (IEditCommand Command, Route Second)? Split(TrailData data, Route route, TrailPoint point)
    {
        var index = IndexOf(route.Points, point);
        if (index == 0 || index == route.Points.Count - 1)
        {
            return null;
        }

        var second = new Route { Name = Numbered(route.Name, 2), Comment = route.Comment, Description = route.Description, Type = route.Type };
        second.Points.Add(Copy(point));
        second.Points.AddRange(route.Points.Skip(index + 1));
        var commands = new List<IEditCommand>
        {
            new ReplaceItemsCommand<TrailPoint>(route.Points, route.Points.Take(index + 1).ToList()),
            new InsertItemCommand<Route>(data.Routes, data.Routes.IndexOf(route) + 1, second),
        };
        return (new CompositeCommand(commands), second);
    }

    /// <summary>
    /// Removes every point before <paramref name="point"/> in its track or route (also the earlier segments of a track),
    /// so it starts there. Returns the command and the number of points removed, or null if there are none.
    /// </summary>
    public static (IEditCommand Command, int Removed)? RemoveBefore(object item, TrailPoint point) => item switch
    {
        Track track => Trim(track, point, before: true),
        Route route => TrimRoute(route, point, before: true),
        _ => throw new ArgumentException("Only tracks and routes can be trimmed.", nameof(item)),
    };

    /// <summary>Removes every point after <paramref name="point"/> (also the later segments of a track), so it ends there.</summary>
    public static (IEditCommand Command, int Removed)? RemoveAfter(object item, TrailPoint point) => item switch
    {
        Track track => Trim(track, point, before: false),
        Route route => TrimRoute(route, point, before: false),
        _ => throw new ArgumentException("Only tracks and routes can be trimmed.", nameof(item)),
    };

    /// <summary>
    /// Makes one track of each segment of <paramref name="track"/>: it keeps the first segment and the others become
    /// new tracks right after it. Null when the track has a single segment.
    /// </summary>
    public static (IEditCommand Command, IReadOnlyList<Track> Tracks)? SegmentsToTracks(TrailData data, Track track)
    {
        if (track.Segments.Count < 2)
        {
            return null;
        }

        var index = data.Tracks.IndexOf(track);
        var created = new List<Track>();
        var commands = new List<IEditCommand>
        {
            new ReplaceItemsCommand<TrackSegment>(track.Segments, [track.Segments[0]]),
        };
        for (var s = 1; s < track.Segments.Count; s++)
        {
            var copy = CopyHeader(track, s + 1);
            copy.Segments.Add(track.Segments[s]);
            created.Add(copy);
            commands.Add(new InsertItemCommand<Track>(data.Tracks, index + s, copy));
        }

        return (new CompositeCommand(commands), created);
    }

    /// <summary>A copy of a point with all its fields (the original stays where it was).</summary>
    public static TrailPoint Copy(TrailPoint point)
    {
        var copy = new TrailPoint(point.Position)
        {
            Elevation = point.Elevation,
            Time = point.Time,
            Name = point.Name,
            Comment = point.Comment,
            Description = point.Description,
            Symbol = point.Symbol,
            Type = point.Type,
            IsAdded = point.IsAdded,
            ElevationSource = point.ElevationSource,
        };
        copy.OtherElements.AddRange(point.OtherElements);
        return copy;
    }

    /// <summary>"Name (2)"; null stays null.</summary>
    public static string? Numbered(string? name, int number) => name is null ? null : $"{name} ({number})";

    private static (IEditCommand Command, int Removed)? Trim(Track track, TrailPoint point, bool before)
    {
        var (segmentIndex, pointIndex) = Find(track, point);
        var segment = track.Segments[segmentIndex].Points;
        var otherSegments = before ? track.Segments.Take(segmentIndex) : track.Segments.Skip(segmentIndex + 1);
        var removed = (before ? pointIndex : segment.Count - pointIndex - 1) + otherSegments.Sum(s => s.Points.Count);
        if (removed == 0)
        {
            return null;
        }

        var keptPoints = before ? segment.Skip(pointIndex).ToList() : segment.Take(pointIndex + 1).ToList();
        var keptSegments = before ? track.Segments.Skip(segmentIndex).ToList() : track.Segments.Take(segmentIndex + 1).ToList();
        IEditCommand command = new CompositeCommand(
        [
            new ReplaceItemsCommand<TrailPoint>(segment, keptPoints),
            new ReplaceItemsCommand<TrackSegment>(track.Segments, keptSegments),
        ]);
        return (command, removed);
    }

    private static (IEditCommand Command, int Removed)? TrimRoute(Route route, TrailPoint point, bool before)
    {
        var index = IndexOf(route.Points, point);
        var kept = before ? route.Points.Skip(index).ToList() : route.Points.Take(index + 1).ToList();
        var removed = route.Points.Count - kept.Count;
        return removed == 0 ? null : (new ReplaceItemsCommand<TrailPoint>(route.Points, kept), removed);
    }

    private static Track CopyHeader(Track track, int number)
    {
        var copy = new Track { Name = Numbered(track.Name, number), Comment = track.Comment, Description = track.Description, Type = track.Type };
        copy.OtherElements.AddRange(track.OtherElements);
        return copy;
    }

    private static (int Segment, int Point) Find(Track track, TrailPoint point)
    {
        for (var s = 0; s < track.Segments.Count; s++)
        {
            var index = track.Segments[s].Points.FindIndex(p => ReferenceEquals(p, point));
            if (index >= 0)
            {
                return (s, index);
            }
        }

        throw new ArgumentException("The point does not belong to the track.", nameof(point));
    }

    private static int IndexOf(List<TrailPoint> points, TrailPoint point)
    {
        var index = points.FindIndex(p => ReferenceEquals(p, point));
        return index >= 0 ? index : throw new ArgumentException("The point does not belong to the route.", nameof(point));
    }
}
