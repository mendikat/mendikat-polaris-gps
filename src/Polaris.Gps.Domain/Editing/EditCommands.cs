using System.Runtime.InteropServices;
using Polaris.Gps.Domain.Model;

namespace Polaris.Gps.Domain.Editing;

/// <summary>A reversible change of a document (EDIT-05). <see cref="Undo"/> restores exactly the previous state.</summary>
public interface IEditCommand
{
    void Do();

    void Undo();
}

/// <summary>Removes points from one ordered list (segment, route or waypoint list).</summary>
public sealed class RemovePointsCommand : IEditCommand
{
    private readonly List<TrailPoint> _owner;
    private readonly List<(int Index, TrailPoint Point)> _removed;

    public RemovePointsCommand(List<TrailPoint> owner, IEnumerable<TrailPoint> points)
    {
        _owner = owner;
        var set = points.ToHashSet(ReferenceEqualityComparer.Instance);
        _removed = owner.Select((p, i) => (Index: i, Point: p)).Where(x => set.Contains(x.Point)).ToList();
        if (_removed.Count != set.Count)
        {
            throw new ArgumentException("All points must belong to the owner list.", nameof(points));
        }
    }

    public int Count => _removed.Count;

    // Do and Undo make a single pass over the list: removing or inserting the points one by one shifts the list
    // once per point, which takes seconds when simplifying tracks of hundreds of thousands of points.

    public void Do()
    {
        var next = 0;
        var write = 0;
        for (var read = 0; read < _owner.Count; read++)
        {
            if (next < _removed.Count && _removed[next].Index == read)
            {
                next++;
                continue;
            }

            _owner[write++] = _owner[read];
        }

        _owner.RemoveRange(write, _owner.Count - write);
    }

    public void Undo()
    {
        var source = _owner.Count - 1;
        CollectionsMarshal.SetCount(_owner, _owner.Count + _removed.Count);
        var next = _removed.Count - 1;
        for (var target = _owner.Count - 1; target >= 0; target--)
        {
            if (next >= 0 && _removed[next].Index == target)
            {
                _owner[target] = _removed[next--].Point;
            }
            else
            {
                _owner[target] = _owner[source--];
            }
        }
    }
}

/// <summary>Inserts one point at a position of an ordered list.</summary>
public sealed class InsertPointCommand(List<TrailPoint> owner, int index, TrailPoint point) : IEditCommand
{
    public TrailPoint Point => point;

    public void Do() => owner.Insert(index, point);

    public void Undo() => owner.RemoveAt(index);
}

/// <summary>Adds a whole item (track, segment, waypoint…) at the end of its list.</summary>
public sealed class AddItemCommand<T>(List<T> owner, T item) : IEditCommand
    where T : class
{
    public void Do() => owner.Add(item);

    public void Undo()
    {
        if (owner.Count == 0 || !ReferenceEquals(owner[^1], item))
        {
            throw new InvalidOperationException("The added item is no longer the last one.");
        }

        owner.RemoveAt(owner.Count - 1);
    }
}

/// <summary>Removes a whole item (track or route) from its list.</summary>
public sealed class RemoveItemCommand<T>(List<T> owner, T item) : IEditCommand
    where T : class
{
    private int _index = -1;

    public void Do()
    {
        _index = owner.IndexOf(item);
        if (_index < 0)
        {
            throw new InvalidOperationException("Item not found.");
        }

        owner.RemoveAt(_index);
    }

    public void Undo() => owner.Insert(_index, item);
}

/// <summary>Changes the editable values of a point (move, rename, elevation…). Untouched data is not affected.</summary>
public sealed class EditPointCommand : IEditCommand
{
    private readonly TrailPoint _point;
    private readonly PointValues _before;
    private readonly PointValues _after;
    private readonly ElevationSource _sourceBefore;

    public EditPointCommand(TrailPoint point, PointValues after)
    {
        _point = point;
        _before = PointValues.From(point);
        _after = after;
        _sourceBefore = point.ElevationSource;
    }

    public void Do()
    {
        _after.ApplyTo(_point);
        if (_after.Elevation != _before.Elevation)
        {
            _point.ElevationSource = ElevationSource.Edited;
        }
    }

    public void Undo()
    {
        _before.ApplyTo(_point);
        _point.ElevationSource = _sourceBefore;
    }
}

/// <summary>Sets the time of many points at once (TIME-*); undo restores each previous time, including "no time".</summary>
public sealed class SetTimesCommand : IEditCommand
{
    private readonly IReadOnlyList<(TrailPoint Point, DateTimeOffset Time)> _times;
    private readonly DateTimeOffset?[] _previous;

    public SetTimesCommand(IReadOnlyList<(TrailPoint Point, DateTimeOffset Time)> times)
    {
        _times = times;
        _previous = times.Select(t => t.Point.Time).ToArray();
    }

    public void Do()
    {
        foreach (var (point, time) in _times)
        {
            point.Time = time;
        }
    }

    public void Undo()
    {
        for (var i = 0; i < _times.Count; i++)
        {
            _times[i].Point.Time = _previous[i];
        }
    }
}

/// <summary>Several commands applied as one undo step.</summary>
public sealed class CompositeCommand(IReadOnlyList<IEditCommand> commands) : IEditCommand
{
    public void Do()
    {
        foreach (var command in commands)
        {
            command.Do();
        }
    }

    public void Undo()
    {
        for (var i = commands.Count - 1; i >= 0; i--)
        {
            commands[i].Undo();
        }
    }
}
