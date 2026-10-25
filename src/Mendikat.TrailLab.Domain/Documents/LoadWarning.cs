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
}

/// <param name="Count">How many items are affected (e.g. number of timestamps), or 0 if not applicable.</param>
public sealed record LoadWarning(LoadWarningKind Kind, int Count = 0);
