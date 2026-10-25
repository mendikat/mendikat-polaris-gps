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
            Time = a.Time is { } ta && b.Time is { } tb ? ta + (tb - ta) / 2 : null,
            IsAdded = true,
        };
        return new InsertPointCommand(owner, second, point);
    }
}
