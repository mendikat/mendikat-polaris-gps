using System.Diagnostics;
using System.Globalization;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Mapping.Coordinates;

namespace Polaris.Gps.Tests.Mapping;

/// <summary>
/// Reference values (ACC-COORD-01..04). Independent references: the equator/Greenwich point (easting 166021.44 m,
/// a textbook value) and the CN Tower example of the Wikipedia UTM article (17T 630084 4833438).
/// </summary>
public sealed class CoordinateFormatterTests
{
    private static (string ZoneBand, double Easting, double Northing) ParseUtm(string text)
    {
        var parts = text.Split(' ');
        Assert.Equal(3, parts.Length);
        return (parts[0], double.Parse(parts[1], CultureInfo.InvariantCulture), double.Parse(parts[2], CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Equator_at_greenwich_is_31N_166021_0()
    {
        Assert.Equal("31N 166021 0", CoordinateFormatter.FormatUtm(new GeoPosition(0, 0)));
    }

    [Fact]
    public void Cn_tower_matches_the_published_utm_within_one_meter()
    {
        var position = new GeoPosition(43 + 38 / 60.0 + 33.24 / 3600, -(79 + 23 / 60.0 + 13.7 / 3600));

        var (zoneBand, easting, northing) = ParseUtm(CoordinateFormatter.FormatUtm(position));

        Assert.Equal("17T", zoneBand);
        Assert.InRange(easting, 630083, 630085);
        Assert.InRange(northing, 4833437, 4833439);
    }

    [Theory]
    [InlineData(42.8125, -1.6458, "30T")]   // Pamplona
    [InlineData(43.3183, -1.9812, "30T")]   // Donostia
    [InlineData(41.3851, 2.1734, "31T")]    // Barcelona
    [InlineData(28.4636, -16.2518, "28R")]  // Santa Cruz de Tenerife
    [InlineData(-33.8688, 151.2093, "56H")] // Sydney (southern hemisphere)
    [InlineData(60, 5, "32V")]              // Norway exception
    [InlineData(78, 10, "33X")]             // Svalbard
    [InlineData(74, 8, "31X")]              // Svalbard: 31X spans 0°–9° E
    [InlineData(-0.1, 10, "32M")]           // just south of the equator
    public void Zone_and_band_follow_the_utm_rules(double lat, double lon, string expected)
    {
        Assert.Equal(expected, ParseUtm(CoordinateFormatter.FormatUtm(new GeoPosition(lat, lon))).ZoneBand);
    }

    [Fact]
    public void Southern_hemisphere_uses_false_northing()
    {
        var (_, _, northing) = ParseUtm(CoordinateFormatter.FormatUtm(new GeoPosition(-33.8688, 151.2093)));

        Assert.InRange(northing, 6_200_000, 6_300_000);
    }

    [Theory]
    [InlineData(85, 0, "UPS Z ")]
    [InlineData(85, -10, "UPS Y ")]
    [InlineData(-85, -10, "UPS A ")]
    [InlineData(-85, 10, "UPS B ")]
    public void Polar_areas_use_ups(double lat, double lon, string expectedPrefix)
    {
        Assert.StartsWith(expectedPrefix, CoordinateFormatter.FormatUtm(new GeoPosition(lat, lon)));
    }

    [Theory]
    [InlineData(-80, 'C')]
    [InlineData(-0.0001, 'M')]
    [InlineData(0, 'N')]
    [InlineData(43, 'T')]
    [InlineData(72, 'X')]
    [InlineData(84, 'X')]
    public void Band_letters(double lat, char expected)
    {
        Assert.Equal(expected, CoordinateFormatter.Band(lat));
    }

    [Theory]
    [InlineData(43.373086111, -1.752777778, "43º 22' 23,11'' N 1º 45' 10,00'' W")]
    [InlineData(42.8125, -1.6458, "42º 48' 45,00'' N 1º 38' 44,88'' W")]
    [InlineData(-33.8688, 151.2093, "33º 52' 07,68'' S 151º 12' 33,48'' E")]
    [InlineData(0, 0, "0º 00' 00,00'' N 0º 00' 00,00'' E")]
    [InlineData(42.999999, -0.0000001, "43º 00' 00,00'' N 0º 00' 00,00'' E")] // rounding carries into degrees
    public void Dms_uses_spanish_format_latitude_first(double lat, double lon, string expected)
    {
        Assert.Equal(expected, CoordinateFormatter.FormatDms(new GeoPosition(lat, lon)));
    }

    [Fact]
    public void Formatting_takes_well_under_a_millisecond()
    {
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < 10_000; i++)
        {
            var p = new GeoPosition(42 + i * 1e-4, -2 + i * 1e-4);
            _ = CoordinateFormatter.FormatUtm(p);
            _ = CoordinateFormatter.FormatDms(p);
        }

        Assert.True(watch.Elapsed.TotalMilliseconds / 10_000 < 1, $"average {watch.Elapsed.TotalMilliseconds / 10_000} ms");
    }
}
