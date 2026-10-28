using Mendikat.TrailLab.Domain.Editing;
using Mendikat.TrailLab.Domain.Elevation;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Domain.Model;

namespace Mendikat.TrailLab.Tests.Domain;

public sealed class ElevationAdjustmentTests
{
    private static List<TrailPoint> Points(params double?[] elevations) =>
        elevations.Select((e, i) => new TrailPoint(new GeoPosition(42 + i * 0.001, -1.6)) { Elevation = e }).ToList();

    [Fact]
    public void Plan_counts_changes_equal_values_and_points_without_data()
    {
        var points = Points(100, 200, 300, null, 500);
        double?[] dem = [110, 200.004, null, 410, 490];

        var plan = ElevationAdjustment.Plan(points, dem);

        Assert.Equal((5, 3, 1, 1, 1), (plan.Total, plan.Changed, plan.Unchanged, plan.NoData, plan.PreviouslyMissing));
        Assert.Equal((-10.0, 10.0, 0.0), (plan.MinDifference!.Value, plan.MaxDifference!.Value, plan.MeanDifference!.Value));
        Assert.All(points, p => Assert.Equal(ElevationSource.Original, p.ElevationSource)); // nothing applied yet (DEM-03)
    }

    [Fact]
    public void Applying_marks_dem_points_keeps_points_without_data_and_undo_restores_everything()
    {
        var points = Points(100, null, 300);
        var plan = ElevationAdjustment.Plan(points, [150, 250, null]);

        plan.Command!.Do();
        Assert.Equal([150, 250, 300], points.Select(p => p.Elevation));
        Assert.Equal([ElevationSource.Dem, ElevationSource.Dem, ElevationSource.Original], points.Select(p => p.ElevationSource));

        plan.Command.Undo();
        Assert.Equal([100, null, 300], points.Select(p => p.Elevation));
        Assert.All(points, p => Assert.Equal(ElevationSource.Original, p.ElevationSource));
    }

    [Fact]
    public void Nothing_to_change_gives_no_command()
    {
        var plan = ElevationAdjustment.Plan(Points(100, 200), [null, 200]);

        Assert.Null(plan.Command);
        Assert.Null(plan.MinDifference);
    }

    [Fact]
    public void Editing_or_inserting_records_the_elevation_origin()
    {
        var points = Points(100, 200);

        var insertion = PointEditing.CreateInsertion(points, points[0])!;
        insertion.Do();
        Assert.Equal(ElevationSource.Interpolated, points[1].ElevationSource);

        var edit = new EditPointCommand(points[0], PointValues.From(points[0]) with { Elevation = 123 });
        edit.Do();
        Assert.Equal(ElevationSource.Edited, points[0].ElevationSource);
        edit.Undo();
        Assert.Equal(ElevationSource.Original, points[0].ElevationSource);

        var rename = new EditPointCommand(points[2], PointValues.From(points[2]) with { Name = "x" });
        rename.Do();
        Assert.Equal(ElevationSource.Original, points[2].ElevationSource); // elevation untouched
    }
}
