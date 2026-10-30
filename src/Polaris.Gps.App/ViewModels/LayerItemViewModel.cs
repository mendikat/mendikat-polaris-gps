using System.Globalization;
using Polaris.Gps.App.Resources;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;

namespace Polaris.Gps.App.ViewModels;

public enum LayerKind
{
    Track,
    Route,
    Waypoints,
}

/// <summary>
/// One entry of the layers panel (LAY-01): a track, a route or the document's waypoints.
/// Visibility only affects the map; it is not a change of the document.
/// </summary>
public sealed class LayerItemViewModel : ObservableObject
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private bool _isVisible = true;

    public LayerItemViewModel(LayerKind kind, object model)
    {
        Kind = kind;
        Model = model;
    }

    /// <summary>Raised when <see cref="IsVisible"/> changes.</summary>
    public event EventHandler? VisibilityChanged;

    public LayerKind Kind { get; }

    /// <summary>The <see cref="Track"/>, <see cref="Route"/> or the waypoint list.</summary>
    public object Model { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (SetProperty(ref _isVisible, value))
            {
                VisibilityChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string Icon => Kind switch
    {
        LayerKind.Track => "〰",
        LayerKind.Route => "⋯",
        _ => "▲",
    };

    public string Name => Model switch
    {
        Track t => NameOrDefault(t.Name),
        Route r => NameOrDefault(r.Name),
        _ => Strings.WaypointsLayerName,
    };

    /// <summary>Point count and, for tracks and routes, geodesic length (segments measured separately).</summary>
    public string Details => Model switch
    {
        Track t => string.Format(Spanish, Strings.LayerDetailsLine, t.PointCount.ToString("N0", Spanish), (Geodesy.Length(t) / 1000).ToString("N2", Spanish)),
        Route r => string.Format(Spanish, Strings.LayerDetailsLine, r.Points.Count.ToString("N0", Spanish), (Geodesy.Length(r.Points) / 1000).ToString("N2", Spanish)),
        List<TrailPoint> w => string.Format(Spanish, Strings.LayerDetailsPoints, w.Count.ToString("N0", Spanish)),
        _ => string.Empty,
    };

    public int PointCount => Model switch
    {
        Track t => t.PointCount,
        Route r => r.Points.Count,
        List<TrailPoint> w => w.Count,
        _ => 0,
    };

    /// <summary>The point lists of this item with their 0-based segment index (tracks only).</summary>
    public IEnumerable<(List<TrailPoint> Points, int? Segment)> PointLists => Model switch
    {
        Track t => t.Segments.Select((s, i) => (s.Points, (int?)i)),
        Route r => [(r.Points, null)],
        List<TrailPoint> w => [(w, null)],
        _ => [],
    };

    public bool IsSequence => Kind != LayerKind.Waypoints;

    /// <summary>The segments of a track that has more than one, to select or delete them one by one; empty otherwise.</summary>
    public IReadOnlyList<SegmentItemViewModel> Segments => Model is Track { Segments.Count: > 1 } track
        ? track.Segments.Select((s, i) => new SegmentItemViewModel(this, s, i)).ToList()
        : [];

    public bool HasSegments => Segments.Count > 0;

    /// <summary>Updates name and statistics after an edit (EDIT-07).</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(Segments));
        OnPropertyChanged(nameof(HasSegments));
    }

    private static string NameOrDefault(string? name) => string.IsNullOrWhiteSpace(name) ? Strings.UnnamedItem : name;
}

/// <summary>A segment of a track in the layers panel: "Segmento 2 · 340 puntos · 2,10 km".</summary>
public sealed class SegmentItemViewModel(LayerItemViewModel layer, TrackSegment segment, int index)
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    public LayerItemViewModel Layer => layer;

    public TrackSegment Segment => segment;

    /// <summary>0-based position in the track.</summary>
    public int Index => index;

    public string Label => string.Format(
        Spanish, Strings.SegmentLabel, index + 1, segment.Points.Count.ToString("N0", Spanish), (Geodesy.Length(segment.Points) / 1000).ToString("N2", Spanish));

    public string DeleteName => string.Format(Spanish, Strings.DeleteSegmentName, index + 1);
}
