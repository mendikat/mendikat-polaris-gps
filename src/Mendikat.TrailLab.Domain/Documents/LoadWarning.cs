namespace Mendikat.TrailLab.Domain.Documents;

/// <summary>Non-fatal issue found while loading; the UI maps each kind to a user message.</summary>
public enum LoadWarningKind
{
    /// <summary>Timestamps without time zone, read as UTC.</summary>
    TimesWithoutZone,

    /// <summary>GPX 1.0 file; it will be saved as GPX 1.1.</summary>
    Gpx10,

    /// <summary>GPX root without the GPX namespace; read as GPX 1.1.</summary>
    GpxWithoutNamespace,

    /// <summary>The file contains no points.</summary>
    NoData,

    /// <summary>Read from another format (KML, KMZ, TCX: <see cref="LoadWarning.Detail"/>) and converted to GPX (CONV-02).</summary>
    ConvertedFrom,

    /// <summary>KML elements without a GPX equivalent (polygons, overlays, models…) that were not converted (CONV-03).</summary>
    IgnoredFeatures,

    /// <summary>TCX trackpoints without position (indoor activities, pauses) that were skipped (CONV-05).</summary>
    PointsWithoutPosition,

    /// <summary>TCX points with data that GPX cannot hold (distance, speed, power) that was not converted (CONV-05).</summary>
    DataNotConverted,
}

/// <param name="Count">How many items are affected (e.g. number of timestamps), or 0 if not applicable.</param>
/// <param name="Detail">Short technical value for the message (e.g. the source format), or null.</param>
public sealed record LoadWarning(LoadWarningKind Kind, int Count = 0, string? Detail = null);
