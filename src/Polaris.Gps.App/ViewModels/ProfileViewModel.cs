using System.Globalization;
using Polaris.Gps.App.Resources;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Domain.Profiles;

namespace Polaris.Gps.App.ViewModels;

/// <summary>
/// Elevation profile window of one track or route (PROF-01). Stays in sync with the document: edits rebuild it
/// (EDIT-07), the selected point is the profile cursor, and the profile selects points and reports the hovered one (PROF-04).
/// </summary>
public sealed class ProfileViewModel : ObservableObject
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private ElevationProfile _profile;
    private bool _includeSegmentGaps;
    private double? _cursorDistance;
    private double? _hoverDistance;
    private string _hoverText = Strings.ProfileHint;

    public ProfileViewModel(DocumentViewModel document, LayerItemViewModel layer)
    {
        Document = document;
        Layer = layer;
        _profile = Build();
        UpdateCursor();
        document.ContentChanged += OnContentChanged;
        document.SelectionChanged += OnSelectionChanged;
    }

    /// <summary>The track or route no longer exists (deleted): the window closes.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>The point under the pointer changed (null when the pointer leaves); the map marks it.</summary>
    public event EventHandler<TrailPoint?>? HoveredPointChanged;

    public DocumentViewModel Document { get; }

    public LayerItemViewModel Layer { get; }

    public string Title => string.Format(Strings.ProfileWindowTitle, Layer.Name, Document.FileName);

    public ElevationProfile Profile
    {
        get => _profile;
        private set
        {
            if (SetProperty(ref _profile, value))
            {
                OnPropertyChanged(nameof(RouteStats));
                OnPropertyChanged(nameof(TimeStats));
                OnPropertyChanged(nameof(MissingWarning));
                OnPropertyChanged(nameof(HasMissingWarning));
                OnPropertyChanged(nameof(NoTimesNote));
            }
        }
    }

    /// <summary>Adds the distance between segments (off by default, PROF-02).</summary>
    public bool IncludeSegmentGaps
    {
        get => _includeSegmentGaps;
        set
        {
            if (SetProperty(ref _includeSegmentGaps, value))
            {
                Profile = Build();
                UpdateCursor();
            }
        }
    }

    public bool HasSeveralSegments => Layer.Model is Track { Segments.Count: > 1 };

    public double? CursorDistance
    {
        get => _cursorDistance;
        private set => SetProperty(ref _cursorDistance, value);
    }

    public double? HoverDistance
    {
        get => _hoverDistance;
        private set => SetProperty(ref _hoverDistance, value);
    }

    /// <summary>Readout of the point under the pointer, or a usage hint.</summary>
    public string HoverText
    {
        get => _hoverText;
        private set => SetProperty(ref _hoverText, value);
    }

    /// <summary>Distance, ascent, descent, lowest and highest elevation (PROF-02, PROF-06); "—" without elevations.</summary>
    public IReadOnlyList<ProfileStat> RouteStats
    {
        get
        {
            var elevation = Profile.HasElevation;
            return
            [
                new(ProfileStatKind.Distance, Strings.StatDistance, $"{(Profile.TotalDistance / 1000).ToString("N2", Spanish)} km"),
                new(ProfileStatKind.Ascent, Strings.StatAscent, elevation ? $"+{Profile.Ascent.ToString("N0", Spanish)} m" : NoValue),
                new(ProfileStatKind.Descent, Strings.StatDescent, elevation ? $"−{Profile.Descent.ToString("N0", Spanish)} m" : NoValue),
                new(ProfileStatKind.MinElevation, Strings.StatMinElevation, Meters(Profile.MinElevation)),
                new(ProfileStatKind.MaxElevation, Strings.StatMaxElevation, Meters(Profile.MaxElevation)),
            ];
        }
    }

    /// <summary>Total, moving and stopped time, average moving speed and sustained maximum speed (PROF-07, PROF-08); empty when the points have no times (see <see cref="NoTimesNote"/>).</summary>
    public IReadOnlyList<ProfileStat> TimeStats => Profile.Times is { } times
        ?
        [
            new(ProfileStatKind.TotalTime, Strings.StatTotalTime, Duration(times.Total)),
            new(ProfileStatKind.MovingTime, Strings.StatMovingTime, Duration(times.Moving)),
            new(ProfileStatKind.StoppedTime, Strings.StatStoppedTime, Duration(times.Stopped)),
            new(ProfileStatKind.AverageSpeed, Strings.StatAverageSpeed, Speed(times.AverageSpeed)),
            new(ProfileStatKind.MaxSpeed, Strings.StatMaxSpeed, Speed(times.MaxSpeed)),
        ]
        : [];

    /// <summary>Shown instead of the time figures when the points have no times.</summary>
    public string? NoTimesNote => Profile.Times is null ? Strings.ProfileNoTimes : null;

    private const string NoValue = "—";

    private static string Meters(double? meters) => meters is { } m ? $"{m.ToString("N0", Spanish)} m" : NoValue;

    /// <summary>"4,3 km/h" from meters per second.</summary>
    private static string Speed(double? metersPerSecond) =>
        metersPerSecond is { } v ? $"{(v * 3.6).ToString("N1", Spanish)} km/h" : NoValue;

    /// <summary>"2 h 05 min", "12 min 30 s".</summary>
    public static string Duration(TimeSpan duration) =>
        duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours} h {duration.Minutes:00} min"
            : $"{duration.Minutes} min {duration.Seconds:00} s";

    /// <summary>"N de M puntos no tienen elevación" when some (not all) are missing (PROF-03).</summary>
    public string? MissingWarning =>
        Profile.MissingElevations > 0 && Profile.HasElevation
            ? string.Format(Spanish, Strings.ProfileMissing, Profile.MissingElevations, Profile.Samples.Count)
            : null;

    public bool HasMissingWarning => MissingWarning is not null;

    /// <summary>Pointer over the profile at <paramref name="distance"/> meters, or left it (null).</summary>
    public void Hover(double? distance)
    {
        var sample = distance is { } d ? Profile.Nearest(d) : null;
        HoverDistance = sample?.Distance;
        HoverText = sample is null ? Strings.ProfileHint : Describe(sample);
        HoveredPointChanged?.Invoke(this, sample?.Point);
    }

    /// <summary>Click on the profile: selects the nearest point in the list and on the map.</summary>
    public void SelectAt(double distance)
    {
        if (Profile.Nearest(distance) is { } sample)
        {
            Document.SelectPoints([sample.Point]);
        }
    }

    /// <summary>Selects the previous (-1) or next (+1) point; with no selection, the first one.</summary>
    public void Step(int direction)
    {
        var samples = Profile.Samples;
        if (samples.Count == 0)
        {
            return;
        }

        var current = Document.SelectedRows.Count == 1 ? Profile.Find(Document.SelectedRows[0].Point) : null;
        var index = current is null ? 0 : Math.Clamp(IndexOf(current) + direction, 0, samples.Count - 1);
        Document.SelectPoints([samples[index].Point]);
    }

    /// <summary>Stops listening to the document (window closed).</summary>
    public void Detach()
    {
        Document.ContentChanged -= OnContentChanged;
        Document.SelectionChanged -= OnSelectionChanged;
        HoveredPointChanged?.Invoke(this, null);
    }

    private int IndexOf(ProfileSample sample)
    {
        for (var i = 0; i < Profile.Samples.Count; i++)
        {
            if (ReferenceEquals(Profile.Samples[i], sample))
            {
                return i;
            }
        }

        return 0;
    }

    private string Describe(ProfileSample sample)
    {
        var elevation = sample.Elevation is { } e ? $"{e.ToString("N1", Spanish)} m" : Strings.ProfileElevationMissingValue;
        var number = HasSeveralSegments ? $"{sample.Segment + 1}·{sample.Index + 1}" : (sample.Index + 1).ToString(Spanish);
        return string.Format(Spanish, Strings.ProfileHover, (sample.Distance / 1000).ToString("N3", Spanish), elevation, number);
    }

    private ElevationProfile Build() => Layer.Model switch
    {
        Track track => ElevationProfile.Build(track, IncludeSegmentGaps),
        Route route => ElevationProfile.Build(route),
        _ => ElevationProfile.Build([]),
    };

    private void UpdateCursor() =>
        CursorDistance = Document.SelectedRows.Count == 1 ? Profile.Find(Document.SelectedRows[0].Point)?.Distance : null;

    private void OnContentChanged(object? sender, EventArgs e)
    {
        if (!Document.Layers.Contains(Layer))
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        Profile = Build();
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(HasSeveralSegments));
        UpdateCursor();
    }

    private void OnSelectionChanged(object? sender, EventArgs e) => UpdateCursor();
}

/// <summary>What a figure of the profile summary measures; the view picks its icon from it.</summary>
public enum ProfileStatKind
{
    Distance,
    Ascent,
    Descent,
    MinElevation,
    MaxElevation,
    TotalTime,
    MovingTime,
    StoppedTime,
    AverageSpeed,
    MaxSpeed,
}

/// <summary>One figure of the profile summary: "+267 m" labelled "Desnivel positivo".</summary>
public sealed record ProfileStat(ProfileStatKind Kind, string Label, string Value)
{
    /// <summary>"Desnivel positivo: +267 m", read by screen readers.</summary>
    public string Description => $"{Label}: {Value}";
}
