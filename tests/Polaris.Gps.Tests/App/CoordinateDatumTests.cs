using System.Globalization;
using Polaris.Gps.App.Services;
using Polaris.Gps.App.Settings;
using Polaris.Gps.App.ViewModels;
using Polaris.Gps.Domain.Documents;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Mapping.Coordinates;
using Polaris.Gps.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Polaris.Gps.Tests.App;

/// <summary>File › Settings › Coordinates: WGS84 (default) or ED50 for the coordinates shown and typed.</summary>
public sealed class CoordinateDatumTests
{
    /// <summary>Aitzgorri (Gipuzkoa), UTM zone 30.</summary>
    private static readonly GeoPosition Aitzgorri = new(42.9512, -2.3386);

    [Fact]
    public void Wgs84_is_the_default_and_unknown_names_fall_back_to_it()
    {
        Assert.Equal("WGS84", new UserSettings().Datum);
        Assert.Equal(CoordinateDatum.Wgs84, CoordinateDatums.FromName(null));
        Assert.Equal(CoordinateDatum.Wgs84, CoordinateDatums.FromName("NAD27"));
        Assert.Equal(CoordinateDatum.Ed50, CoordinateDatums.FromName(" ed50 "));
        Assert.False(new UserSettings().Map.ShowUtmGrid);
    }

    [Fact]
    public void Ed50_utm_is_shifted_about_100_m_east_and_200_m_north_in_Spain()
    {
        var wgs84 = Utm(CoordinateFormatter.FormatUtm(Aitzgorri, CoordinateDatum.Wgs84));
        var ed50 = Utm(CoordinateFormatter.FormatUtm(Aitzgorri, CoordinateDatum.Ed50));

        Assert.Equal(wgs84.Zone, ed50.Zone);
        Assert.InRange(ed50.Easting - wgs84.Easting, 90, 130);
        Assert.InRange(ed50.Northing - wgs84.Northing, 180, 230);
    }

    [Fact]
    public void Ed50_round_trips_and_outside_Spain_and_Portugal_coordinates_stay_in_wgs84()
    {
        var (lat, lon) = CoordinateDatums.FromWgs84(Aitzgorri, CoordinateDatum.Ed50);
        var back = CoordinateDatums.ToWgs84(lat, lon, CoordinateDatum.Ed50);
        // The inverse reverses the signs of the parameters, as mendikat.net does: not an exact inverse, a few centimetres off.
        Assert.Equal(Aitzgorri.Latitude, back.Latitude, 1e-6);
        Assert.Equal(Aitzgorri.Longitude, back.Longitude, 1e-6);

        var tenerife = new GeoPosition(28.27, -16.64); // zone 28: no ED50 parameters
        Assert.Equal(CoordinateDatum.Wgs84, CoordinateDatums.Effective(CoordinateDatum.Ed50, tenerife));
        Assert.Equal(CoordinateFormatter.FormatUtm(tenerife), CoordinateFormatter.FormatUtm(tenerife, CoordinateDatum.Ed50));
    }

    [Fact]
    public void The_point_detail_shows_and_reads_ed50_and_keeps_the_exact_position_when_untouched()
    {
        var point = new TrailPoint(Aitzgorri) { Name = "Aitzgorri" };
        var detail = new PointDetailViewModel(point, CoordinateDatum.Ed50) { Name = "Aizkorri" };

        var (ed50Lat, ed50Lon) = CoordinateDatums.FromWgs84(Aitzgorri, CoordinateDatum.Ed50);
        Assert.Equal(PointInput.FormatCoordinate(ed50Lat), detail.Latitude);
        Assert.Equal(PointInput.FormatCoordinate(ed50Lon), detail.Longitude);
        Assert.True(detail.TryBuild(out var renamed));
        Assert.Equal(Aitzgorri, renamed.Position);

        detail.Latitude = PointInput.FormatCoordinate(ed50Lat + 0.001);
        Assert.True(detail.TryBuild(out var moved));
        var expected = CoordinateDatums.ToWgs84(ed50Lat + 0.001, ed50Lon, CoordinateDatum.Ed50);
        Assert.Equal(expected.Latitude, moved.Position.Latitude, 1e-7);
        Assert.Equal(expected.Longitude, moved.Position.Longitude, 1e-7);
    }

    [Fact]
    public async Task Choosing_ed50_changes_the_status_bar_and_the_points_list_at_once_and_is_saved()
    {
        var dialogs = new FakeDialogService();
        var settings = new FakeSettingsStore();
        var vm = new MainViewModel(dialogs, new FakeDocumentStore(), settings, NullLogger<MainViewModel>.Instance);
        dialogs.OpenPaths.Enqueue(@"C:\data\a.gpx");
        await vm.OpenAsync(DocumentFormat.Gpx);
        var row = vm.ActiveDocument!.Rows[0];
        var wgs84Latitude = row.Latitude;

        dialogs.ToolDialog = dialog =>
        {
            var settingsDialog = Assert.IsType<SettingsViewModel>(dialog);
            Assert.Equal(["WGS84", "ED50"], settingsDialog.Datums);
            settingsDialog.Datum = "ED50";
            settingsDialog.ShowUtmGrid = true;
            return true;
        };
        vm.ShowSettings();

        Assert.Equal("ED50", settings.Current.Datum);
        Assert.True(settings.Current.Map.ShowUtmGrid);
        Assert.True(vm.ShowUtmGrid);
        Assert.Equal("ED50", vm.PointerDatum);
        var (ed50Lat, _) = PointInput.FormatPosition(row.Point.Position, CoordinateDatum.Ed50);
        Assert.Equal(ed50Lat, vm.ActiveDocument.Rows[0].Latitude);
        Assert.NotEqual(wgs84Latitude, vm.ActiveDocument.Rows[0].Latitude);

        vm.UpdatePointer(Aitzgorri);
        Assert.Equal(CoordinateFormatter.FormatUtm(Aitzgorri, CoordinateDatum.Ed50), vm.PointerUtm);
        vm.UpdatePointer(new GeoPosition(28.27, -16.64));
        Assert.Equal("WGS84", vm.PointerDatum);
    }

    private static (string Zone, double Easting, double Northing) Utm(string text)
    {
        var parts = text.Split(' ');
        return (parts[0], double.Parse(parts[1], CultureInfo.InvariantCulture), double.Parse(parts[2], CultureInfo.InvariantCulture));
    }
}
