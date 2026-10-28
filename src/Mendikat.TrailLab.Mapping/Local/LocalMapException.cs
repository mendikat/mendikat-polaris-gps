namespace Mendikat.TrailLab.Mapping.Local;

/// <summary>Reason a local map or DEM could not be loaded (LMAP-02). The UI maps each value to a Spanish message.</summary>
public enum LocalMapError
{
    FileNotFound,
    EmptyFile,
    ReadFailed,

    /// <summary>Not one of the supported formats (.map, .imp, .asc, .hgt). Detail: the extension.</summary>
    UnsupportedFormat,

    /// <summary>Calibration incomplete or inconsistent. Detail: what is wrong.</summary>
    InvalidCalibration,

    /// <summary>The image the calibration refers to does not exist. Detail: its path as written in the file.</summary>
    ImageMissing,

    /// <summary>The image is in a format Windows cannot decode (OZF, ECW…). Detail: the image file name.</summary>
    UnsupportedImage,

    /// <summary>The image could not be decoded. Detail: the image file name.</summary>
    InvalidImage,

    /// <summary>Datum or projection outside the supported list (LMAP-04). Detail: its name.</summary>
    UnsupportedCrs,

    /// <summary>An ASCII grid without its .prj file (D10). Detail: the expected .prj name.</summary>
    MissingProjection,

    /// <summary>A DEM file with a wrong size, header or values. Detail: what is wrong.</summary>
    InvalidDem,

    /// <summary>Dimensions too large to hold in memory. Detail: the dimensions.</summary>
    TooLarge,

    /// <summary>GDAL (GeoTIFF, ECW) could not be loaded. Detail: why.</summary>
    GdalUnavailable,

    /// <summary>An ECW file or stream, but the ECW plugin is not available. Detail: the file name.</summary>
    EcwUnavailable,

    /// <summary>The user has not accepted the ECW licence (ECW/ECWP are disabled until then).</summary>
    EcwLicenseNotAccepted,

    /// <summary>A raster without georeferencing (no coordinate system or transformation). Detail: the file name.</summary>
    NotGeoreferenced,
}

/// <summary>Expected failure while loading a local map or DEM; carries a typed reason instead of a UI text.</summary>
public sealed class LocalMapException(LocalMapError error, string? detail = null, int? line = null, Exception? innerException = null)
    : Exception($"Local map load failed: {error}{(line is null ? string.Empty : $" at line {line}")}{(detail is null ? string.Empty : $" ({detail})")}", innerException)
{
    public LocalMapError Error { get; } = error;

    /// <summary>Short technical detail (value, file name). Never whole file content.</summary>
    public string? Detail { get; } = detail;

    /// <summary>1-based line of the calibration or grid file, when known.</summary>
    public int? Line { get; } = line;
}
