using System.Globalization;
using Mendikat.TrailLab.App.Resources;
using Mendikat.TrailLab.Domain.Model;
using Mendikat.TrailLab.Domain.Profiles;

namespace Mendikat.TrailLab.App.ViewModels;

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
                OnPropertyChanged(nameof(Summary));
                OnPropertyChanged(nameof(MissingWarning));
                OnPropertyChanged(nameof(HasMissingWarning));
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

    public string Summary
    {
        get
        {
            var km = (Profile.TotalDistance / 1000).ToString("N2", Spanish);
            return Profile.HasElevation
                ? string.Format(Spanish, Strings.ProfileSummary, km, Profile.MinElevation!.Value.ToString("N0", Spanish), Profile.MaxElevation!.Value.ToString("N0", Spanish))
                : string.Format(Spanish, Strings.ProfileSummaryNoElevation, km);
        }
    }

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
