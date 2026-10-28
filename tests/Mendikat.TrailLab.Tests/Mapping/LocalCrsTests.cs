using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Mapping.Local;

namespace Mendikat.TrailLab.Tests.Mapping;

/// <summary>Coordinate systems of local maps and DEMs (LMAP-04).</summary>
public sealed class LocalCrsTests
{
    /// <summary>1e-7° ≈ 1 cm.</summary>
    private const double Tolerance = 1e-7;

    /// <summary>
    /// mendikat.net stops iterating the geocentric → geodetic latitude at 1e-6 rad and returns the previous iterate,
    /// about 0.3 m off (D12). TrailLab converges fully, so the WGS84 latitude is compared within 1e-5° (about 1 m).
    /// </summary>
    private const double PhpLatitudeTolerance = 1e-5;

    /// <summary>
    /// Reference values computed with mendikat.net (<c>Geoposition::fromUtm(..., Datum::ED50())-&gt;toWGS84()</c>,
    /// PHP 8.5, 2026-10-27): UTM ED50 → geographic ED50 → geographic WGS84.
    /// </summary>
    [Theory]
    [InlineData(30, 430000, 4780000, 43.168743769, -3.861126318, 43.167638726, -3.862461647)]
    [InlineData(30, 600000, 4700000, 42.445125873, -1.784080252, 42.444010244, -1.785351872)]
    [InlineData(30, 350000, 4100000, 37.033632266, -4.686447190, 37.032359154, -4.687750423)]
    [InlineData(31, 500000, 4600000, 41.550935869, 3.000000000, 41.549827986, 2.998882432)]
    [InlineData(29, 550000, 4700000, 42.449962885, -8.391990236, 42.448790137, -8.393496404)]
    public void Ed50_utm_gives_the_same_wgs84_as_mendikat_net(
        int zone, double easting, double northing, double ed50Lat, double ed50Lon, double wgsLat, double wgsLon)
    {
        var crs = Crs.Utm(zone, north: true, MapDatum.Ed50);

        var (lat, lon) = crs.ToGeographic(easting, northing);
        var wgs84 = crs.ToWgs84(easting, northing);

        Assert.Equal(ed50Lat, lat, Tolerance);
        Assert.Equal(ed50Lon, lon, Tolerance);
        Assert.Equal(wgsLon, wgs84.Longitude, Tolerance);
        Assert.Equal(wgsLat, wgs84.Latitude, PhpLatitudeTolerance);
    }

    [Theory]
    [InlineData(MapDatum.Ed50)]
    [InlineData(MapDatum.Wgs84)]
    [InlineData(MapDatum.Etrs89)]
    public void Wgs84_round_trips_through_every_datum(MapDatum datum)
    {
        var crs = Crs.Utm(30, north: true, datum);
        var position = new GeoPosition(42.8123, -1.6456);

        var (x, y) = crs.FromWgs84(position);
        var back = crs.ToWgs84(x, y);

        // ED50: the reversed Helmert parameters (as in mendikat.net) are not an exact inverse; a few millimetres.
        Assert.Equal(position.Latitude, back.Latitude, 1e-6);
        Assert.Equal(position.Longitude, back.Longitude, 1e-6);
    }

    [Fact]
    public void Utm_wgs84_matches_the_status_bar_formatter()
    {
        // 30T 610926 4740868 is what the status bar shows for this point (COORD-01).
        var (x, y) = Crs.Utm(30, north: true, MapDatum.Wgs84).FromWgs84(new GeoPosition(42.81234, -1.64321));

        Assert.Equal(610926, Math.Round(x));
        Assert.Equal(4740868, Math.Round(y));
    }

    [Fact]
    public void Ed50_outside_zones_29_to_31_is_not_supported()
    {
        var ex = Assert.Throws<UnsupportedCrsException>(() => Crs.Utm(32, north: true, MapDatum.Ed50).ToWgs84(500000, 5000000));

        Assert.Contains("32", ex.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_transverse_mercator_with_latitude_of_origin_is_offset_from_it()
    {
        var tm = Crs.TransverseMercator(MapDatum.Wgs84, latitudeOfOrigin: 40, centralMeridian: -3, scale: 1, falseEasting: 100000, falseNorthing: 200000);

        var (x, y) = tm.FromGeographic(40, -3);

        Assert.Equal(100000, x, 1e-6);
        Assert.Equal(200000, y, 1e-6);
    }
}
