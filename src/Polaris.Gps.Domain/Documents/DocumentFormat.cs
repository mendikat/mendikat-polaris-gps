namespace Polaris.Gps.Domain.Documents;

/// <summary>
/// File format a document was loaded from. A document is always saved back in this format (SAVE-01).
/// Only GPX since 2026-10-26 (WPT support was removed); the type is kept so other formats can be added.
/// </summary>
public enum DocumentFormat
{
    Gpx,
}
