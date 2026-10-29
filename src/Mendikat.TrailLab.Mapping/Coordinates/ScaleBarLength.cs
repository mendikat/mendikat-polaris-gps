using System.Globalization;

namespace Mendikat.TrailLab.Mapping.Coordinates;

/// <summary>
/// Length of the map scale bar: the longest round distance (1, 2 or 5 × 10ⁿ meters) that fits in a given width, in
/// ground meters at the latitude of the map center (Web Mercator stretches distances by 1 / cos(latitude)).
/// </summary>
/// <param name="Meters">Distance the bar represents.</param>
/// <param name="Pixels">Width of the bar on screen.</param>
/// <param name="Segments">Alternating black and white blocks: 4 for a leading 2, otherwise 5.</param>
public readonly record struct ScaleBarLength(double Meters, double Pixels, int Segments)
{
    /// <summary>"200 m", "1 km", "20 km".</summary>
    public string Label => Meters >= 1000
        ? (Meters / 1000).ToString("0.##", CultureInfo.CurrentCulture) + " km"
        : Meters.ToString("0.##", CultureInfo.CurrentCulture) + " m";

    /// <param name="resolution">Web Mercator meters per pixel.</param>
    /// <param name="centerY">Web Mercator Y of the map center.</param>
    /// <param name="maxPixels">Widest bar allowed.</param>
    /// <returns>Null if the view has no usable resolution (not laid out yet).</returns>
    public static ScaleBarLength? For(double resolution, double centerY, double maxPixels)
    {
        var latitude = double.IsFinite(centerY) ? WebMercator.ToGeo(0, centerY).Latitude : 0;
        var metersPerPixel = resolution * Math.Cos(latitude * Math.PI / 180);
        if (!double.IsFinite(metersPerPixel) || metersPerPixel <= 0 || !(maxPixels > 0))
        {
            return null;
        }

        var widest = metersPerPixel * maxPixels;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(widest)));
        var leading = widest / magnitude;
        var step = leading >= 5 ? 5 : leading >= 2 ? 2 : 1;
        // Rounded so that 0.1 × 2 is 0.2 and not 0.20000000000000001.
        var meters = Math.Round(step * magnitude, 6);
        return new ScaleBarLength(meters, meters / metersPerPixel, step == 2 ? 4 : 5);
    }
}
