using System.Globalization;
using GeographicLib;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Mapping.Coordinates;
using Polaris.Gps.Mapping.Local;

namespace Polaris.Gps.Mapping.Layers;

/// <summary>A line of the UTM grid, as WGS84 positions, with its label (kilometres or metres) placed at its first point.</summary>
public sealed record GridLine(IReadOnlyList<GeoPosition> Points, string Label, bool IsEasting);

/// <summary>
/// The UTM grid of the visible area (File › Settings › Display), in the UTM zone of the centre of the view and in the
/// coordinate datum (WGS84, or ED50 inside zones 29–31). The spacing is a round distance that keeps the lines at
/// least <see cref="MinimumPixels"/> apart. Lines are sampled, so they follow the slight curvature of UTM on the map.
/// </summary>
public static class UtmGrid
{
    /// <summary>Lines closer than this on screen would clutter the map: a wider spacing is chosen.</summary>
    public const double MinimumPixels = 90;

    /// <summary>Zoomed out beyond this resolution (meters per pixel) the grid is not drawn.</summary>
    public const double MaxResolution = 1500;

    private const int Samples = 16;

    private static readonly double[] Spacings = [100, 200, 500, 1000, 2000, 5000, 10_000, 20_000, 50_000, 100_000];

    /// <summary>The spacing in meters for a resolution (meters per pixel at the centre of the view).</summary>
    public static double SpacingFor(double resolution) =>
        Spacings.FirstOrDefault(s => s / resolution >= MinimumPixels, Spacings[^1]);

    /// <summary>The grid lines covering <paramref name="view"/>; empty when zoomed out too far or outside UTM (poles).</summary>
    /// <param name="resolution">Ground meters per screen pixel at the centre of the view.</param>
    public static IReadOnlyList<GridLine> Lines(GeoBounds view, double resolution, CoordinateDatum datum)
    {
        var centre = new GeoPosition((view.MinLatitude + view.MaxLatitude) / 2, (view.MinLongitude + view.MaxLongitude) / 2);
        if (resolution > MaxResolution || centre.Latitude is < -80 or > 84)
        {
            return [];
        }

        var effective = CoordinateDatums.Effective(datum, centre);
        var zone = effective == CoordinateDatum.Ed50 ? Ed50.ZoneOf(centre.Longitude) : UTMUPS.StandardZone(centre.Latitude, centre.Longitude);
        var north = centre.Latitude >= 0;
        var projection = new Projection(zone, north, effective);

        // UTM extent of the view: project its border.
        double minE = double.MaxValue, maxE = double.MinValue, minN = double.MaxValue, maxN = double.MinValue;
        for (var i = 0; i <= Samples; i++)
        {
            var t = i / (double)Samples;
            foreach (var p in new[]
            {
                new GeoPosition(view.MinLatitude, view.MinLongitude + t * (view.MaxLongitude - view.MinLongitude)),
                new GeoPosition(view.MaxLatitude, view.MinLongitude + t * (view.MaxLongitude - view.MinLongitude)),
                new GeoPosition(view.MinLatitude + t * (view.MaxLatitude - view.MinLatitude), view.MinLongitude),
                new GeoPosition(view.MinLatitude + t * (view.MaxLatitude - view.MinLatitude), view.MaxLongitude),
            })
            {
                var (e, n) = projection.Forward(p);
                (minE, maxE, minN, maxN) = (Math.Min(minE, e), Math.Max(maxE, e), Math.Min(minN, n), Math.Max(maxN, n));
            }
        }

        var spacing = SpacingFor(resolution);
        var lines = new List<GridLine>();
        for (var e = Math.Ceiling(minE / spacing) * spacing; e <= maxE; e += spacing)
        {
            var points = Enumerable.Range(0, Samples + 1).Select(i => projection.Reverse(e, minN + i * (maxN - minN) / Samples)).ToList();
            lines.Add(new GridLine(points, Label(e, spacing), IsEasting: true));
        }

        for (var n = Math.Ceiling(minN / spacing) * spacing; n <= maxN; n += spacing)
        {
            var points = Enumerable.Range(0, Samples + 1).Select(i => projection.Reverse(minE + i * (maxE - minE) / Samples, n)).ToList();
            lines.Add(new GridLine(points, Label(n, spacing), IsEasting: false));
        }

        return lines;
    }

    /// <summary>Kilometres for spacings of 1 km or more ("4750"), meters otherwise ("4750300").</summary>
    private static string Label(double value, double spacing) =>
        spacing >= 1000
            ? (value / 1000).ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0", CultureInfo.InvariantCulture);

    /// <summary>UTM of one zone on WGS84 (GeographicLib) or ED50 (International 1924, Helmert of mendikat.net).</summary>
    private sealed class Projection(int zone, bool north, CoordinateDatum datum)
    {
        private readonly Crs? _ed50 = datum == CoordinateDatum.Ed50 ? Crs.Utm(zone, north, MapDatum.Ed50) : null;

        public (double Easting, double Northing) Forward(GeoPosition position)
        {
            if (_ed50 is not null)
            {
                return _ed50.FromWgs84(position);
            }

            var (_, _, x, y) = UTMUPS.Forward(position.Latitude, position.Longitude, zone, false);
            return (x, y);
        }

        public GeoPosition Reverse(double easting, double northing)
        {
            if (_ed50 is not null)
            {
                return _ed50.ToWgs84(easting, northing);
            }

            var (lat, lon) = UTMUPS.Reverse(zone, north, easting, northing);
            return new GeoPosition(Math.Clamp(lat, -90, 90), lon);
        }
    }
}
