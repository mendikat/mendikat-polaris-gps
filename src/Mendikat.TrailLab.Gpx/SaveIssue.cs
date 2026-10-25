namespace Mendikat.TrailLab.Gpx;

/// <summary>Something the user should know before saving because the file will differ from the model (WPE-04, GPX-04).</summary>
public enum SaveIssueKind
{
    /// <summary>A GPX 1.0 document is written as GPX 1.1; fields without equivalent go to "extensions".</summary>
    GpxVersionUpgrade,

    /// <summary>Characters that the file encoding (e.g. Windows-1252) cannot represent; written as "?".</summary>
    UnrepresentableCharacters,

    /// <summary>CompeGPS waypoints without elevation are written with elevation 0 (the format has no "empty").</summary>
    MissingElevationWrittenAsZero,
}

/// <param name="Count">Number of affected items, or 0 if not applicable.</param>
public sealed record SaveIssue(SaveIssueKind Kind, int Count = 0);
