using Polaris.Gps.Domain.Editing;
using Polaris.Gps.Domain.Model;

namespace Polaris.Gps.Domain.Processing;

/// <summary>
/// Reverses the direction of a track or route (REV-01): the order of the segments and of the points in each segment.
/// Segments are not merged and points keep all their fields. Times are mirrored so the result is still chronological
/// (REV-02): <c>t' = t_first + t_last − t</c>, which keeps the start and end times and every interval between points.
/// </summary>
public static class TrackReverser
{
    /// <summary>One undoable step that reverses <paramref name="track"/>.</summary>
    public static IEditCommand Reverse(Track track)
    {
        var commands = new List<IEditCommand> { new ReverseOrderCommand<TrackSegment>(track.Segments) };
        commands.AddRange(track.Segments.Select(s => new ReverseOrderCommand<TrailPoint>(s.Points)));
        AddMirroredTimes(commands, track.AllPoints);
        return new CompositeCommand(commands);
    }

    /// <summary>One undoable step that reverses <paramref name="route"/>.</summary>
    public static IEditCommand Reverse(Route route)
    {
        var commands = new List<IEditCommand> { new ReverseOrderCommand<TrailPoint>(route.Points) };
        AddMirroredTimes(commands, route.Points);
        return new CompositeCommand(commands);
    }

    /// <summary>Points without time keep having none.</summary>
    private static void AddMirroredTimes(List<IEditCommand> commands, IEnumerable<TrailPoint> points)
    {
        var timed = points.Where(p => p.Time is not null).ToList();
        if (timed.Count == 0)
        {
            return;
        }

        var first = timed.Min(p => p.Time!.Value).ToUniversalTime();
        var last = timed.Max(p => p.Time!.Value);
        commands.Add(new SetTimesCommand(timed.Select(p => (p, first + (last - p.Time!.Value))).ToList()));
    }
}

/// <summary>Reverses the order of a list; doing it twice restores it, so undo is the same operation.</summary>
public sealed class ReverseOrderCommand<T>(List<T> owner) : IEditCommand
{
    public void Do() => owner.Reverse();

    public void Undo() => owner.Reverse();
}
