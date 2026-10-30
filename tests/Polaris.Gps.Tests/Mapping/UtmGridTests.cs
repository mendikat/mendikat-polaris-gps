using System.Globalization;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Mapping.Coordinates;
using Polaris.Gps.Mapping.Layers;

namespace Polaris.Gps.Tests.Mapping;

/// <summary>The UTM grid over the map (File › Settings › Display), in WGS84 or ED50.</summary>
public sealed class UtmGridTests
{
    /// <summary>About 4 × 3 km around Aitzgorri (zone 30).</summary>
    private static readonly GeoBounds View = new(42.94, -2.36, 42.97, -2.31);

    [Fact]
    public void The_spacing_keeps_the_lines_apart_on_screen()
    {
        Assert.Equal(1000, UtmGrid.SpacingFor(10)); // 100 px per km
        Assert.Equal(200, UtmGrid.SpacingFor(2));
        Assert.Equal(100_000, UtmGrid.SpacingFor(5000));
    }

    [Fact]
    public void Lines_fall_on_round_utm_values_of_the_zone_of_the_view()
    {
        var lines = UtmGrid.Lines(View, 10, CoordinateDatum.Wgs84);

        var eastings = lines.Where(l => l.IsEasting).ToList();
        Assert.InRange(eastings.Count, 3, 6);
        Assert.InRange(lines.Count(l => !l.IsEasting), 2, 5);
        foreach (var line in eastings)
        {
            var easting = int.Parse(line.Label, CultureInfo.InvariantCulture) * 1000;
            var utm = CoordinateFormatter.FormatUtm(line.Points[8]).Split(' ');
            Assert.Equal("30T", utm[0]);
            Assert.InRange(int.Parse(utm[1], CultureInfo.InvariantCulture), easting - 1, easting + 1);
        }
    }

    [Fact]
    public void The_ed50_grid_is_shifted_from_the_wgs84_one_and_none_is_drawn_zoomed_out()
    {
        var wgs84 = UtmGrid.Lines(View, 10, CoordinateDatum.Wgs84).First(l => l.IsEasting);
        var ed50 = UtmGrid.Lines(View, 10, CoordinateDatum.Ed50).First(l => l.Label == wgs84.Label && l.IsEasting);

        var shift = Geodesy.Distance(wgs84.Points[8], ed50.Points[8]);
        Assert.InRange(shift, 60, 160); // the ED50 line of the same value is about 100 m west
        Assert.Empty(UtmGrid.Lines(View, UtmGrid.MaxResolution * 2, CoordinateDatum.Wgs84));
    }
}
