namespace Mendikat.TrailLab.Domain.Documents;

/// <summary>File format a document was loaded from. A document is always saved back in this format (SAVE-01).</summary>
public enum DocumentFormat
{
    Gpx,
    Wpt,
}
