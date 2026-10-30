namespace Polaris.Gps.App.Charting;

/// <summary>"Nice" axis graduations: steps of 1, 2 or 5 × 10ⁿ covering a range with at most a given number of ticks.</summary>
public static class AxisTicks
{
    public static double NiceStep(double range, int maxTicks)
    {
        if (range <= 0 || maxTicks < 1 || !double.IsFinite(range))
        {
            return 1;
        }

        var rough = range / maxTicks;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        foreach (var factor in new[] { 1.0, 2.0, 5.0, 10.0 })
        {
            if (factor * magnitude >= rough)
            {
                return factor * magnitude;
            }
        }

        return 10 * magnitude;
    }

    /// <summary>Multiples of the nice step within [min, max].</summary>
    public static IReadOnlyList<double> Ticks(double min, double max, int maxTicks)
    {
        var step = NiceStep(max - min, maxTicks);
        var first = Math.Ceiling(min / step) * step;
        var ticks = new List<double>();
        for (var value = first; value <= max + step * 1e-9; value += step)
        {
            ticks.Add(Math.Round(value / step) * step);
        }

        return ticks;
    }
}
