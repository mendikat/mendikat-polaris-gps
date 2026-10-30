namespace Polaris.Gps.Gpx;

/// <summary>Something the user should know before saving because the file will differ from the model (WPE-04, GPX-04).</summary>
public enum SaveIssueKind
{
    /// <summary>A GPX 1.0 document is written as GPX 1.1; fields without equivalent go to "extensions".</summary>
    GpxVersionUpgrade,
}

/// <param name="Count">Number of affected items, or 0 if not applicable.</param>
public sealed record SaveIssue(SaveIssueKind Kind, int Count = 0);
