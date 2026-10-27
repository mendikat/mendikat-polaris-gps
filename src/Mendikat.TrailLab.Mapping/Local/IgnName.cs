using System.Globalization;
using System.Text.RegularExpressions;

namespace Mendikat.TrailLab.Mapping.Local;

/// <summary>
/// Coordinate system stated by the name of an IGN digital terrain model (LDEM-04, D10): "MDT05-0112-H30-LIDAR",
/// "MDT25-0112-H30" (huso 30, ETRS89) or "PNOA_MDT05_ETRS89_HU30_0112_LID", "PNOA_MDT05_REGCAN95_HU28_1096_LID".
/// The IGN distributes them in UTM, ETRS89 on the Peninsula and Balearics and REGCAN95 in the Canary Islands.
/// </summary>
public static partial class IgnName
{
    /// <returns>The UTM system of the name, or null if it is not an IGN terrain model name.</returns>
    public static Crs? Crs(string fileNameWithoutExtension)
    {
        var name = fileNameWithoutExtension.Trim();
        var match = Pattern().Match(name);
        if (!match.Success)
        {
            return null;
        }

        var zone = int.Parse(match.Groups["zone"].Value, CultureInfo.InvariantCulture);
        if (zone is < 1 or > 60)
        {
            return null;
        }

        var datum = name.Contains("REGCAN95", StringComparison.OrdinalIgnoreCase) ? MapDatum.Regcan95 : MapDatum.Etrs89;
        return Local.Crs.Utm(zone, north: true, datum);
    }

    [GeneratedRegex(@"^(?:PNOA_)?MDT\d{2}[-_].*?(?:^|[-_])HU?(?<zone>\d{2})(?:[-_ (]|$)", RegexOptions.IgnoreCase)]
    private static partial Regex Pattern();
}
