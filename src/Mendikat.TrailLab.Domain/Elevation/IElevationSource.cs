using Mendikat.TrailLab.Domain.Geo;

namespace Mendikat.TrailLab.Domain.Elevation;

/// <summary>
/// A digital elevation model (DEM-01). Kept apart from the UI and the file formats; implementations may be online
/// services or local files (ADR-0007). Positions are WGS84.
/// </summary>
public interface IElevationSource
{
    /// <summary>Name shown to the user, e.g. "MDT IGN 5 m".</summary>
    string Name { get; }

    /// <summary>Vertical reference of the values (DEM-05), shown in the preview. No geoid correction is applied.</summary>
    string VerticalDatum { get; }

    /// <summary>Mandatory attribution of the data.</summary>
    string Attribution { get; }

    /// <summary>One elevation per position, in meters, or null where the model has no data.</summary>
    /// <param name="progress">Fraction completed, 0 to 1.</param>
    /// <exception cref="ElevationSourceException">The source could not be queried (network, service error).</exception>
    /// <exception cref="OperationCanceledException">The user cancelled.</exception>
    Task<IReadOnlyList<double?>> GetElevationsAsync(
        IReadOnlyList<GeoPosition> positions, IProgress<double>? progress, CancellationToken cancellationToken);
}

/// <summary>The elevation source failed; nothing has been changed.</summary>
public sealed class ElevationSourceException(string message, Exception? innerException = null) : Exception(message, innerException);
