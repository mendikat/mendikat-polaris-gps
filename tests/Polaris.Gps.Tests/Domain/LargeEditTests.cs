using Polaris.Gps.Domain.Editing;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;

namespace Polaris.Gps.Tests.Domain;

/// <summary>Removing many points at once must stay exact and fast on very large tracks (NFR-02, increment 8).</summary>
public sealed class LargeEditTests
{
    private static List<TrailPoint> Points(int count) =>
        Enumerable.Range(0, count).Select(i => new TrailPoint(new GeoPosition(42 + i * 1e-6, -1.6))).ToList();

    [Theory]
    [InlineData(1, 0.5)]
    [InlineData(10, 0.0)]
    [InlineData(10, 1.0)]
    [InlineData(1000, 0.1)]
    [InlineData(1000, 0.9)]
    public void Removing_any_subset_and_undoing_restores_the_exact_list(int count, double fraction)
    {
        var random = new Random(count);
        var owner = Points(count);
        var original = owner.ToList();
        var removed = owner.Where((_, i) => i == 0 || i == count - 1 || random.NextDouble() < fraction).ToList();
        var expected = original.Except(removed).ToList();
        var command = new RemovePointsCommand(owner, removed);

        command.Do();
        Assert.Equal(expected, owner);

        command.Undo();
        Assert.Equal(original, owner);

        command.Do();
        Assert.Equal(expected, owner);
    }

    [Fact]
    public void Removing_half_of_500000_points_and_undoing_takes_well_under_a_second()
    {
        var owner = Points(500_000);
        var removed = owner.Where((_, i) => i % 2 == 1).ToList();
        var command = new RemovePointsCommand(owner, removed);
        var watch = System.Diagnostics.Stopwatch.StartNew();

        command.Do();
        command.Undo();

        Assert.Equal(500_000, owner.Count);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), $"took {watch.Elapsed}");
    }
}
