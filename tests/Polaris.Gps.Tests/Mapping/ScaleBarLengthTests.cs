using System.Globalization;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Mapping.Coordinates;

namespace Polaris.Gps.Tests.Mapping;

/// <summary>The map scale bar: a round distance that fits, measured on the ground at the latitude of the center.</summary>
public sealed class ScaleBarLengthTests
{
    [Theory]
    [InlineData(1.0, 200, 200, 4)] // 220 m fit: 200 m in 200 px
    [InlineData(2.0, 200, 100, 4)] // 440 m: 200 m
    [InlineData(5.0, 1000, 200, 5)] // 1100 m: 1 km
    [InlineData(0.3, 50, 166.67, 5)] // 66 m: 50 m
    [InlineData(100.0, 20000, 200, 4)] // 22 km: 20 km
    public void At_the_equator_the_longest_round_distance_that_fits_is_chosen(double resolution, double meters, double pixels, int segments)
    {
        var scale = ScaleBarLength.For(resolution, 0, 220)!.Value;

        Assert.Equal(meters, scale.Meters);
        Assert.Equal(pixels, scale.Pixels, 0.01);
        Assert.Equal(segments, scale.Segments);
        Assert.InRange(scale.Pixels, 88, 220);
    }

    [Fact]
    public void Away_from_the_equator_ground_distances_are_shorter_than_Web_Mercator_ones()
    {
        // At 60° a Web Mercator meter is half a meter on the ground.
        var (_, y) = WebMercator.ToWorld(new GeoPosition(60, -2));

        var scale = ScaleBarLength.For(2.0, y, 220)!.Value;

        Assert.Equal(200, scale.Meters, 1e-9);
        Assert.Equal(200, scale.Pixels, 1e-6);
    }

    [Fact]
    public void Kilometres_are_used_from_1000_m()
    {
        Assert.Equal("200 m", new ScaleBarLength(200, 100, 4).Label);
        Assert.Equal("1 km", new ScaleBarLength(1000, 100, 5).Label);
        Assert.Equal("50 km", new ScaleBarLength(50_000, 100, 5).Label);
    }

    [Fact]
    public void Fractions_of_a_meter_use_the_decimal_separator_of_the_language()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es");
            var scale = ScaleBarLength.For(0.002, 0, 220)!.Value;
            Assert.Equal(0.2, scale.Meters);
            Assert.Equal("0,2 m", scale.Label);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(double.NaN)]
    [InlineData(-1.0)]
    public void A_view_without_resolution_has_no_scale_bar(double resolution) =>
        Assert.Null(ScaleBarLength.For(resolution, 0, 220));
}
