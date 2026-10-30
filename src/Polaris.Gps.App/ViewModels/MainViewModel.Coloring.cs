using System.Globalization;
using Polaris.Gps.App.Resources;
using Polaris.Gps.Mapping.Layers;
using Coloring = Polaris.Gps.Mapping.Layers.TrackColoring;

namespace Polaris.Gps.App.ViewModels;

/// <summary>One colour of the map legend: "#D7281E" and its meaning ("15 a 25 %", "máx.").</summary>
public sealed record LegendItem(string ColorHex, string Label);

/// <summary>View › Colour tracks by: elevation, slope or speed on the map, with a legend; remembered between sessions.</summary>
public sealed partial class MainViewModel
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private TrackColorMode _trackColoring;

    public RelayCommand TrackColoringCommand { get; private set; } = null!;

    /// <summary>How the tracks are coloured on the map; the map view redraws when it changes.</summary>
    public TrackColorMode TrackColoring
    {
        get => _trackColoring;
        set
        {
            if (!SetProperty(ref _trackColoring, value))
            {
                return;
            }

            OnPropertyChanged(nameof(Legend));
            OnPropertyChanged(nameof(LegendTitle));
            OnPropertyChanged(nameof(HasLegend));
            OnPropertyChanged(nameof(IsColoringNone));
            OnPropertyChanged(nameof(IsColoringElevation));
            OnPropertyChanged(nameof(IsColoringSlope));
            OnPropertyChanged(nameof(IsColoringSpeed));
            var current = _settings.Load();
            if (!_settings.Save(current with { Map = current.Map with { TrackColoring = value.ToString() } }))
            {
                StatusMessage = Strings.StatusMapSaveSettingsFailed;
            }
        }
    }

    // Check marks of the View › Colour tracks by menu.
    public bool IsColoringNone => TrackColoring == TrackColorMode.None;

    public bool IsColoringElevation => TrackColoring == TrackColorMode.Elevation;

    public bool IsColoringSlope => TrackColoring == TrackColorMode.Slope;

    public bool IsColoringSpeed => TrackColoring == TrackColorMode.Speed;

    public bool HasLegend => TrackColoring != TrackColorMode.None;

    public string LegendTitle => TrackColoring switch
    {
        TrackColorMode.Elevation => Strings.LegendElevation,
        TrackColorMode.Slope => Strings.LegendSlope,
        TrackColorMode.Speed => Strings.LegendSpeed,
        _ => string.Empty,
    };

    /// <summary>Slope: its fixed classes; elevation and speed: the ramp from the lowest to the highest value of each track.</summary>
    public IReadOnlyList<LegendItem> Legend => TrackColoring switch
    {
        TrackColorMode.None => [],
        TrackColorMode.Slope => SlopeLegend(),
        _ => Enumerable.Range(0, Coloring.RampSteps)
            .Select(i => new LegendItem(Hex(Coloring.RampColor(i)),
                i == 0 ? Strings.LegendLow : i == Coloring.RampSteps - 1 ? Strings.LegendHigh : string.Empty))
            .ToList(),
    };

    private static List<LegendItem> SlopeLegend()
    {
        var classes = Coloring.SlopeClasses;
        var items = new List<LegendItem>();
        for (var i = 0; i < classes.Count; i++)
        {
            var label = i == 0 ? $"< {Percent(classes[0].UpTo)}"
                : i == classes.Count - 1 ? $"> {Percent(classes[i - 1].UpTo)}"
                : $"{Percent(classes[i - 1].UpTo)} … {Percent(classes[i].UpTo)}";
            items.Add(new LegendItem(Hex(classes[i].Color), label));
        }

        return items;
    }

    private static string Percent(double value) => $"{value.ToString("0", Invariant).Replace('-', '−')} %";

    private static string Hex(Rgb color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private void InitializeColoringCommands() =>
        TrackColoringCommand = new RelayCommand(p =>
        {
            if (Enum.TryParse<TrackColorMode>(p as string, out var mode))
            {
                TrackColoring = mode;
            }

            return Task.CompletedTask;
        });
}
