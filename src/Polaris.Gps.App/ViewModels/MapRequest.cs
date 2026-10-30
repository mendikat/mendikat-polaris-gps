using Polaris.Gps.Domain.Geo;

namespace Polaris.Gps.App.ViewModels;

/// <summary>Navigation the view model asks the map view to perform.</summary>
public abstract record MapRequest
{
    public sealed record ZoomIn : MapRequest;

    public sealed record ZoomOut : MapRequest;

    /// <summary>Fit the map to <paramref name="Bounds"/> (MAP-09).</summary>
    public sealed record ZoomTo(GeoBounds Bounds) : MapRequest;
}
