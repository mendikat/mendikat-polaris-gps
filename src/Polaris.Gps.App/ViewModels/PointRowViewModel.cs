using Polaris.Gps.App.Services;
using Polaris.Gps.Domain.Model;

namespace Polaris.Gps.App.ViewModels;

/// <summary>A row of the points list. Texts are computed on demand so large tracks build quickly.</summary>
public sealed class PointRowViewModel(TrailPoint point, List<TrailPoint> owner, int index, int? segment, bool showSegment)
{
    public TrailPoint Point => point;

    /// <summary>The list containing the point (segment, route or waypoints).</summary>
    public List<TrailPoint> Owner => owner;

    /// <summary>1-based position in its list.</summary>
    public int Index => index + 1;

    /// <summary>"15", or "2·15" (segment 2, point 15) when the track has several segments.</summary>
    public string Number => showSegment ? $"{segment + 1}·{index + 1}" : (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    public string Name => point.Name ?? string.Empty;

    public string Latitude => PointInput.FormatCoordinate(point.Position.Latitude);

    public string Longitude => PointInput.FormatCoordinate(point.Position.Longitude);

    public string Elevation => PointInput.FormatElevation(point.Elevation);

    public string Time => PointInput.FormatLocal(point.Time);

    public bool IsAdded => point.IsAdded;
}
