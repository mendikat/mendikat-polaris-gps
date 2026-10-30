using Polaris.Gps.Domain.Editing;
using Polaris.Gps.Domain.Model;

namespace Polaris.Gps.Domain.Elevation;

/// <summary>Preview of an elevation adjustment (DEM-02) and the undoable command that applies it (DEM-03).</summary>
/// <param name="Changed">Points whose elevation will change.</param>
/// <param name="Unchanged">Points whose DEM value equals their current elevation (within 1 cm).</param>
/// <param name="NoData">Points without DEM value: they keep their elevation (DEM-04).</param>
/// <param name="MinDifference">Smallest DEM − original difference over points that had an elevation, or null.</param>
public sealed record ElevationPlan(
    int Total,
    int Changed,
    int Unchanged,
    int NoData,
    int PreviouslyMissing,
    double? MinDifference,
    double? MaxDifference,
    double? MeanDifference,
    IEditCommand? Command);

/// <summary>Builds the elevation adjustment from DEM values (DEM-02..06).</summary>
public static class ElevationAdjustment
{
    /// <summary>Differences below this are considered equal (the DEM has centimetre resolution at best).</summary>
    public const double Tolerance = 0.01;

    public static ElevationPlan Plan(IReadOnlyList<TrailPoint> points, IReadOnlyList<double?> demValues)
    {
        if (points.Count != demValues.Count)
        {
            throw new ArgumentException("One DEM value per point is required.", nameof(demValues));
        }

        var changes = new List<(TrailPoint Point, double Elevation)>();
        var differences = new List<double>();
        int unchanged = 0, noData = 0, previouslyMissing = 0;

        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            if (demValues[i] is not { } dem)
            {
                noData++;
                continue;
            }

            if (point.Elevation is { } original)
            {
                if (Math.Abs(dem - original) < Tolerance)
                {
                    unchanged++;
                    continue;
                }

                differences.Add(dem - original);
            }
            else
            {
                previouslyMissing++;
            }

            changes.Add((point, dem));
        }

        return new ElevationPlan(
            points.Count,
            changes.Count,
            unchanged,
            noData,
            previouslyMissing,
            differences.Count > 0 ? differences.Min() : null,
            differences.Count > 0 ? differences.Max() : null,
            differences.Count > 0 ? differences.Average() : null,
            changes.Count > 0 ? new SetElevationsCommand(changes) : null);
    }
}

/// <summary>Sets DEM elevations; undo restores each previous elevation (including none) and its origin (DEM-03, DEM-06).</summary>
public sealed class SetElevationsCommand : IEditCommand
{
    private readonly IReadOnlyList<(TrailPoint Point, double Elevation)> _changes;
    private readonly (double? Elevation, ElevationSource Source)[] _previous;
    private readonly ElevationSource _source;

    /// <param name="source">Origin recorded for the new elevations: the DEM by default, or the smoothing of the cleaning tool.</param>
    public SetElevationsCommand(IReadOnlyList<(TrailPoint Point, double Elevation)> changes, ElevationSource source = ElevationSource.Dem)
    {
        _changes = changes;
        _source = source;
        _previous = changes.Select(c => (c.Point.Elevation, c.Point.ElevationSource)).ToArray();
    }

    public void Do()
    {
        foreach (var (point, elevation) in _changes)
        {
            point.Elevation = elevation;
            point.ElevationSource = _source;
        }
    }

    public void Undo()
    {
        for (var i = 0; i < _changes.Count; i++)
        {
            (_changes[i].Point.Elevation, _changes[i].Point.ElevationSource) = _previous[i];
        }
    }
}
