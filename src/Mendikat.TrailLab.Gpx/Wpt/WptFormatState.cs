using System.Text;
using Mendikat.TrailLab.Domain.Editing;

namespace Mendikat.TrailLab.Gpx.Wpt;

/// <summary>Document-level data of a WPT file, kept to write it back in the same dialect and encoding.</summary>
public sealed class WptFormatState
{
    public required string DialectId { get; init; }

    public required Encoding Encoding { get; init; }

    /// <summary>Lines before the first waypoint, verbatim.</summary>
    public required IReadOnlyList<string> HeaderLines { get; init; }
}

/// <summary>Per-waypoint WPT data: the original lines, so unedited fields are saved verbatim (WPT-04).</summary>
public sealed class WptPointState
{
    /// <summary>The waypoint line followed by any associated lines (CompeGPS "w", blank lines…), verbatim.</summary>
    public required IReadOnlyList<string> Lines { get; init; }

    /// <summary>Fields of the waypoint line as split by the dialect.</summary>
    public required IReadOnlyList<string> Fields { get; init; }

    /// <summary>Values as read; the writer compares against them to rewrite only edited fields.</summary>
    public PointValues? Original { get; set; }
}
