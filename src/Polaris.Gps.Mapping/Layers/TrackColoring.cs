using Polaris.Gps.Domain.Model;
using Polaris.Gps.Domain.Profiles;

namespace Polaris.Gps.Mapping.Layers;

/// <summary>What colours the tracks on the map (View › Colour tracks by). Routes always keep the document colour.</summary>
public enum TrackColorMode
{
    None,
    Elevation,
    Slope,
    Speed,
}

/// <summary>A colour as red, green and blue bytes, shared by the map (Mapsui) and the profile chart (WPF).</summary>
public readonly record struct Rgb(byte R, byte G, byte B);

/// <summary>One entry of the colour legend: the colour and what it means ("> 25 %", "baja"…).</summary>
public sealed record LegendEntry(Rgb Color, string Label);

/// <summary>
/// The colour classes of a track coloured by elevation, slope or speed. Slope uses fixed classes (the same on every
/// track, as in the profile chart); elevation and speed use a blue → green → yellow → red ramp from the lowest to the
/// highest value of each track (speed from the 5th to the 95th percentile, so a GPS jump does not flatten the rest).
/// </summary>
public static class TrackColoring
{
    /// <summary>Number of colour steps of the elevation and speed ramp.</summary>
    public const int RampSteps = 8;

    /// <summary>Slope classes, in percent: each colour applies up to its limit; the last one to anything steeper.</summary>
    public static IReadOnlyList<(double UpTo, Rgb Color)> SlopeClasses { get; } =
    [
        (-15, new Rgb(30, 60, 150)),
        (-8, new Rgb(50, 120, 210)),
        (-3, new Rgb(110, 180, 230)),
        (3, new Rgb(60, 170, 60)),
        (8, new Rgb(235, 200, 0)),
        (15, new Rgb(240, 130, 0)),
        (25, new Rgb(215, 40, 30)),
        (double.PositiveInfinity, new Rgb(130, 0, 60)),
    ];

    public static int SlopeClass(double slope)
    {
        for (var i = 0; i < SlopeClasses.Count; i++)
        {
            if (slope <= SlopeClasses[i].UpTo)
            {
                return i;
            }
        }

        return SlopeClasses.Count - 1;
    }

    public static Rgb SlopeColor(double slope) => SlopeClasses[SlopeClass(slope)].Color;

    /// <summary>Colour of step <paramref name="step"/> (0 … <see cref="RampSteps"/> − 1) of the ramp: blue, green, yellow, red.</summary>
    public static Rgb RampColor(int step)
    {
        var t = Math.Clamp(step / (double)(RampSteps - 1), 0, 1);
        (double R, double G, double B)[] stops = [(40, 90, 220), (40, 170, 70), (240, 210, 0), (215, 40, 30)];
        var position = t * (stops.Length - 1);
        var index = Math.Min((int)position, stops.Length - 2);
        var f = position - index;
        var (a, b) = (stops[index], stops[index + 1]);
        return new Rgb((byte)Math.Round(a.R + (b.R - a.R) * f), (byte)Math.Round(a.G + (b.G - a.G) * f), (byte)Math.Round(a.B + (b.B - a.B) * f));
    }

    /// <summary>Number of colour classes of a mode.</summary>
    public static int ClassCount(TrackColorMode mode) => mode == TrackColorMode.Slope ? SlopeClasses.Count : RampSteps;

    public static Rgb ClassColor(TrackColorMode mode, int index) => mode == TrackColorMode.Slope ? SlopeClasses[index].Color : RampColor(index);

    /// <summary>
    /// The colour class of each point of the track, in track order (all segments), or −1 where the point has no value
    /// (no elevation, no time); null for <see cref="TrackColorMode.None"/> or when no point has a value.
    /// </summary>
    public static int[]? Classify(Track track, TrackColorMode mode)
    {
        if (mode == TrackColorMode.None || track.PointCount < 2)
        {
            return null;
        }

        var profile = ElevationProfile.Build(track);
        var values = mode switch
        {
            TrackColorMode.Elevation => profile.Samples.Select(s => s.Elevation).ToArray(),
            TrackColorMode.Slope => profile.Analysis.Slopes.ToArray(),
            _ => profile.Analysis.Speeds.ToArray(),
        };
        var present = values.OfType<double>().Order().ToList();
        if (present.Count == 0)
        {
            return null;
        }

        if (mode == TrackColorMode.Slope)
        {
            return values.Select(v => v is { } slope ? SlopeClass(slope) : -1).ToArray();
        }

        var (low, high) = mode == TrackColorMode.Speed
            ? (Percentile(present, 0.05), Percentile(present, 0.95))
            : (present[0], present[^1]);
        var range = high - low;
        return values.Select(v => v is { } value
            ? range <= 0 ? RampSteps / 2 : Math.Clamp((int)((value - low) / range * RampSteps), 0, RampSteps - 1)
            : -1).ToArray();
    }

    private static double Percentile(List<double> sorted, double p) => sorted[(int)Math.Round((sorted.Count - 1) * p)];
}
