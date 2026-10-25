using System.Text;

namespace Mendikat.TrailLab.Gpx.Wpt;

/// <summary>
/// One WPT dialect (WPT-01, ADR-0006). Dialects are detected by content, not by extension (WPT-07),
/// so new ones can be added by implementing this interface and registering it in <see cref="WaypointFileReader"/>.
/// </summary>
public interface IWaypointFileFormat
{
    /// <summary>Stable identifier stored in <see cref="WptFormatState.DialectId"/>.</summary>
    string DialectId { get; }

    /// <summary>True if the file content looks like this dialect.</summary>
    bool CanRead(IReadOnlyList<string> lines);

    /// <exception cref="Domain.Documents.DocumentLoadException">The content is invalid or unsupported.</exception>
    TrailFileResult Read(IReadOnlyList<string> lines, Encoding encoding);
}
