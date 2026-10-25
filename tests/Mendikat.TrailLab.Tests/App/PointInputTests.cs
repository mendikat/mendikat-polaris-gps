using Mendikat.TrailLab.App.Services;
using Mendikat.TrailLab.App.ViewModels;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Domain.Model;

namespace Mendikat.TrailLab.Tests.App;

public sealed class PointInputTests
{
    [Theory]
    [Trait("Req", "WPE-03")]
    [InlineData("42,5", 42.5)]
    [InlineData("42.5", 42.5)]
    [InlineData(" -1,6458 ", -1.6458)]
    public void Numbers_accept_decimal_comma_or_point(string text, double expected)
    {
        Assert.True(PointInput.TryParseNumber(text, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [Trait("Req", "WPE-03")]
    [InlineData("1.234,5")] // thousands separators are rejected, never read as 1234.5 or 1.2345
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("1e3")]
    public void Ambiguous_or_invalid_numbers_are_rejected(string text)
    {
        Assert.False(PointInput.TryParseNumber(text, out _));
    }

    [Theory]
    [Trait("Req", "WPE-03")]
    [Trait("Req", "EDIT-04")]
    [InlineData("90", true)]
    [InlineData("-90", true)]
    [InlineData("90,0001", false)]
    [InlineData("91", false)]
    public void Latitude_must_be_within_90_degrees(string text, bool valid)
    {
        Assert.Equal(valid, PointInput.TryParseLatitude(text, out _));
    }

    [Theory]
    [Trait("Req", "WPE-03")]
    [InlineData("180", true)]
    [InlineData("-180", true)]
    [InlineData("180,5", false)]
    public void Longitude_must_be_within_180_degrees(string text, bool valid)
    {
        Assert.Equal(valid, PointInput.TryParseLongitude(text, out _));
    }

    [Theory]
    [Trait("Req", "WPE-03")]
    [InlineData("", true, null)]
    [InlineData("1431,5", true, 1431.5)]
    [InlineData("-500", true, -500.0)]
    [InlineData("9001", false, null)]
    [InlineData("x", false, null)]
    public void Elevation_is_optional_and_limited(string text, bool valid, double? expected)
    {
        Assert.Equal(valid, PointInput.TryParseElevation(text, out var value));
        Assert.Equal(expected, value);
    }

    [Fact]
    [Trait("Req", "WPE-03")]
    public void Utc_time_is_parsed_without_time_zone_conversion()
    {
        Assert.True(PointInput.TryParseUtc("2026-05-01 07:30:15", out var time));
        Assert.Equal(new DateTimeOffset(2026, 5, 1, 7, 30, 15, TimeSpan.Zero), time);
        Assert.True(PointInput.TryParseUtc("", out var none));
        Assert.Null(none);
        Assert.False(PointInput.TryParseUtc("01/05/2026", out _));
    }

    [Fact]
    [Trait("Req", "LAY-04")]
    public void Detail_shows_values_and_missing_ones_as_empty_or_dash()
    {
        var point = new TrailPoint(new GeoPosition(42.98530001234, -2.0786)) { Name = "Txindoki" };

        var detail = new PointDetailViewModel(point, PointFieldSupport.Gpx);

        Assert.Equal("42,9853000", detail.Latitude);
        Assert.Equal("", detail.Elevation);
        Assert.Equal("—", detail.LocalTime);
        Assert.Equal("—", detail.OtherFields);
        Assert.False(detail.HasChanges);
    }

    [Fact]
    [Trait("Req", "WPE-02")]
    [Trait("Req", "EDIT-04")]
    public void Untouched_fields_keep_their_exact_original_values()
    {
        var point = new TrailPoint(new GeoPosition(42.98530001234, -2.07860009876)) { Elevation = 1431.123456, Name = "A" };
        var detail = new PointDetailViewModel(point, PointFieldSupport.Gpx) { Name = "B" };

        Assert.True(detail.TryBuild(out var values));

        Assert.Equal("B", values.Name);
        Assert.Equal(point.Position, values.Position); // not rounded to the 7 displayed decimals
        Assert.Equal(1431.123456, values.Elevation);
    }

    [Fact]
    [Trait("Req", "WPE-03")]
    public void Invalid_values_are_not_built_and_an_error_is_shown()
    {
        var detail = new PointDetailViewModel(new TrailPoint(new GeoPosition(42, -1)), PointFieldSupport.Gpx) { Latitude = "95" };

        Assert.False(detail.TryBuild(out _));
        Assert.Equal("La latitud debe ser un número entre −90 y 90.", detail.Error);

        detail.Latitude = "43";
        Assert.Null(detail.Error);
        Assert.True(detail.TryBuild(out var values));
        Assert.Equal(43, values.Position.Latitude);
    }

    [Fact]
    [Trait("Req", "WPE-02")]
    [Trait("Req", "WPE-03")]
    public void Format_restrictions_apply_to_wpt_texts()
    {
        var compe = new PointDetailViewModel(new TrailPoint(new GeoPosition(42, -1)), PointFieldSupport.CompeGps) { Name = "dos palabras" };
        Assert.False(compe.TryBuild(out _));
        Assert.False(compe.ShowComment);

        var ozi = new PointDetailViewModel(new TrailPoint(new GeoPosition(42, -1)), PointFieldSupport.OziExplorer) { Description = "a, b" };
        Assert.False(ozi.TryBuild(out _));
        Assert.Equal("OziExplorer no admite comas en los textos.", ozi.Error);
    }

    [Fact]
    [Trait("Req", "WPE-02")]
    public void Clearing_a_text_removes_the_value()
    {
        var detail = new PointDetailViewModel(new TrailPoint(new GeoPosition(42, -1)) { Description = "x", Elevation = 10 }, PointFieldSupport.Gpx)
        {
            Description = "",
            Elevation = "",
        };

        Assert.True(detail.TryBuild(out var values));
        Assert.Null(values.Description);
        Assert.Null(values.Elevation);
    }
}
