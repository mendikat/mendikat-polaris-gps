using System.Globalization;
using Polaris.Gps.App.Resources;
using Polaris.Gps.App.Services;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Domain.Processing;

namespace Polaris.Gps.App.ViewModels;

/// <summary>One track that can be joined, with the document it belongs to.</summary>
public sealed class JoinTrackItem : ObservableObject
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private bool _isSelected = true;

    public JoinTrackItem(Track track, string documentName)
    {
        Track = track;
        DocumentName = documentName;
    }

    /// <summary>Raised when <see cref="IsSelected"/> changes; the preview is recomputed.</summary>
    public event EventHandler? SelectionChanged;

    public Track Track { get; }

    public string DocumentName { get; }

    public string Name => string.IsNullOrWhiteSpace(Track.Name) ? Strings.UnnamedItem : Track.Name;

    /// <summary>"Subida — monte.gpx · 1.234 puntos".</summary>
    public string Label => string.Format(Spanish, Strings.JoinTrackItem, Name, DocumentName, Track.PointCount.ToString("N0", Spanish));

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}

/// <summary>
/// "Join tracks" dialog: the tracks of every open document, the maximum gap inside a segment and whether tracks may be
/// reversed, with a live preview of the order chosen by proximity. Nothing changes until the user accepts; the joined
/// track then opens in a new unsaved GPX document and the original tracks stay as they are.
/// </summary>
public sealed class JoinTracksViewModel : ObservableObject
{
    public const double DefaultMaxGap = 200;

    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private string _maxGapText = DefaultMaxGap.ToString(Spanish);
    private bool _allowReverse = true;
    private JoinPlan? _plan;
    private string? _error;

    /// <param name="tracks">Tracks with points, each with the name of its document.</param>
    public JoinTracksViewModel(IEnumerable<(Track Track, string DocumentName)> tracks)
    {
        Items = tracks.Select(t => new JoinTrackItem(t.Track, t.DocumentName)).ToList();
        foreach (var item in Items)
        {
            item.SelectionChanged += (_, _) => Recompute();
        }

        Recompute();
    }

    public IReadOnlyList<JoinTrackItem> Items { get; }

    public string MaxGapText
    {
        get => _maxGapText;
        set
        {
            if (SetProperty(ref _maxGapText, value ?? string.Empty))
            {
                Recompute();
            }
        }
    }

    public bool AllowReverse
    {
        get => _allowReverse;
        set
        {
            if (SetProperty(ref _allowReverse, value))
            {
                Recompute();
            }
        }
    }

    public JoinPlan? Plan
    {
        get => _plan;
        private set
        {
            if (SetProperty(ref _plan, value))
            {
                OnPropertyChanged(nameof(Steps));
                OnPropertyChanged(nameof(Preview));
                OnPropertyChanged(nameof(CanApply));
            }
        }
    }

    public string? Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    /// <summary>The tracks in the order they are joined: "2. Bajada (invertido) — hueco 35 m".</summary>
    public IReadOnlyList<string> Steps => Plan is { } plan
        ? plan.Steps.Select((s, i) => StepText(plan, s, i + 1)).ToList()
        : [];

    /// <summary>"3 tracks → 1 track de 1.234 puntos en 2 segmentos. Huecos: 1,24 km en total."</summary>
    public string Preview => Plan is { } plan
        ? string.Format(Spanish, Strings.JoinPreview, plan.Steps.Count,
            plan.Steps.Sum(s => plan.Tracks[s.Index].PointCount).ToString("N0", Spanish),
            SegmentCount(plan), Distance(plan.TotalGapMeters))
        : string.Empty;

    public bool CanApply => Plan is not null;

    /// <summary>The joined track (copies of the points), built when the user accepts.</summary>
    public Track Build() => TrackJoiner.Build(Plan ?? throw new InvalidOperationException("Nothing to join."));

    /// <summary>Segments of the joined track: those of every track, minus one per join that continues a segment.</summary>
    public static int SegmentCount(JoinPlan plan) =>
        plan.Steps.Sum(s => plan.Tracks[s.Index].Segments.Count(g => g.Points.Count > 0))
        - plan.Steps.Skip(1).Count(s => !s.StartsSegment);

    /// <summary>"35 m" below one kilometre, "1,24 km" above.</summary>
    public static string Distance(double meters) => meters < 1000
        ? string.Format(Spanish, Strings.JoinDistanceMeters, meters.ToString("N0", Spanish))
        : string.Format(Spanish, Strings.JoinDistanceKilometers, (meters / 1000).ToString("N2", Spanish));

    private string StepText(JoinPlan plan, JoinStep step, int number)
    {
        var item = Items.First(i => ReferenceEquals(i.Track, plan.Tracks[step.Index]));
        var name = step.Reversed ? string.Format(Strings.JoinReversedName, item.Name) : item.Name;
        return step.GapMeters is not { } gap
            ? string.Format(Spanish, Strings.JoinStepFirst, number, name)
            : string.Format(Spanish, step.StartsSegment ? Strings.JoinStepNewSegment : Strings.JoinStep, number, name, Distance(gap));
    }

    private void Recompute()
    {
        var selected = Items.Where(i => i.IsSelected).Select(i => i.Track).ToList();
        if (!PointInput.TryParseNumber(MaxGapText, out var maxGap) || maxGap < 0 || maxGap > 100_000)
        {
            Error = Strings.JoinInvalidGap;
            Plan = null;
            return;
        }

        if (selected.Count < 2)
        {
            Error = Strings.JoinNeedsTwo;
            Plan = null;
            return;
        }

        Error = null;
        Plan = TrackJoiner.Plan(selected, maxGap, AllowReverse);
    }
}
