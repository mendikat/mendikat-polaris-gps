using System.Globalization;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Domain.Model;

namespace Mendikat.TrailLab.Gpx.Wpt;

/// <summary>Parsing helpers shared by the WPT dialects. Line numbers are 1-based.</summary>
internal static class WptParsing
{
    public static bool TryParseDouble(string text, out double value) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    public static double ParseDouble(string text, int line, DocumentLoadError error) =>
        TryParseDouble(text, out var value) ? value : throw new DocumentLoadException(error, text.Trim(), line);

    public static GeoPosition ToPosition(double latitude, double longitude, int line)
    {
        if (!GeoPosition.IsValidLatitude(latitude))
        {
            throw new DocumentLoadException(DocumentLoadError.InvalidCoordinate, $"lat={latitude.ToString(CultureInfo.InvariantCulture)}", line);
        }

        if (!GeoPosition.IsValidLongitude(longitude))
        {
            throw new DocumentLoadException(DocumentLoadError.InvalidCoordinate, $"lon={longitude.ToString(CultureInfo.InvariantCulture)}", line);
        }

        return new GeoPosition(latitude, longitude);
    }

    public static bool IsWgs84(string datum) =>
        datum.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).Equals("WGS84", StringComparison.OrdinalIgnoreCase);

    public static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>Stores the values as read in the point state, so the writer can detect edits.</summary>
    public static TrailPoint RememberOriginal(TrailPoint point)
    {
        ((WptPointState)point.FormatState!).Original = Domain.Editing.PointValues.From(point);
        return point;
    }
}
