using System.IO;
using Polaris.Gps.App.Services;
using Polaris.Gps.App.ViewModels;
using Polaris.Gps.Domain.Documents;
using Polaris.Gps.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Polaris.Gps.Tests.App;

/// <summary>Communications › Send to GPS / Receive from GPS, with folders that look like a Garmin and a TwoNav.</summary>
public sealed class GpsDeviceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "polarisgps-gps-" + Guid.NewGuid().ToString("N")[..8]);

    public GpsDeviceTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Garmin(string? model = "GPSMAP 66s")
    {
        var drive = Path.Combine(_root, "garmin");
        Directory.CreateDirectory(Path.Combine(drive, "Garmin", "GPX", "Current"));
        if (model is not null)
        {
            File.WriteAllText(Path.Combine(drive, "Garmin", "GarminDevice.xml"),
                $"<Device xmlns=\"http://www.garmin.com/xmlschemas/GarminDevice/v2\"><Model><PartNumber>006-B</PartNumber><Description>{model}</Description></Model></Device>");
        }

        return drive;
    }

    [Fact]
    public void A_garmin_is_recognised_by_its_folder_and_named_after_its_model()
    {
        var device = DriveGpsDeviceFinder.Inspect(Garmin(), "GARMIN")!;

        Assert.Equal(GpsDeviceKind.Garmin, device.Kind);
        Assert.StartsWith("GPSMAP 66s (", device.Name);
        Assert.Equal(Path.Combine(device.Root, "Garmin", "GPX"), device.SendFolder);
        Assert.Contains(Path.Combine(device.Root, "Garmin", "GPX", "Current"), device.ReceiveFolders);
    }

    [Fact]
    public void A_twonav_is_recognised_and_other_drives_are_not_gps()
    {
        var twoNav = Path.Combine(_root, "twonav");
        Directory.CreateDirectory(Path.Combine(twoNav, "TwoNavData", "Data"));
        var usb = Path.Combine(_root, "usb");
        Directory.CreateDirectory(usb);

        Assert.Equal(GpsDeviceKind.TwoNav, DriveGpsDeviceFinder.Inspect(twoNav, "TRAIL 2")!.Kind);
        Assert.StartsWith("TRAIL 2 (", DriveGpsDeviceFinder.Inspect(twoNav, "TRAIL 2")!.Name);
        Assert.Null(DriveGpsDeviceFinder.Inspect(usb));
    }

    [Fact]
    public void Only_files_that_can_be_opened_are_listed_newest_first()
    {
        var device = DriveGpsDeviceFinder.Inspect(Garmin(model: null), "GARMIN")!;
        File.WriteAllText(Path.Combine(device.SendFolder, "old.gpx"), "<gpx/>");
        File.SetLastWriteTimeUtc(Path.Combine(device.SendFolder, "old.gpx"), DateTime.UtcNow.AddDays(-3));
        File.WriteAllText(Path.Combine(device.SendFolder, "Current", "Current.gpx"), "<gpx/>");
        File.WriteAllText(Path.Combine(device.SendFolder, "notes.txt"), "x");

        var dialog = new GpsDeviceViewModel(new Finder(device), sending: false);

        Assert.StartsWith("GARMIN (", device.Name);
        Assert.Equal(["Current.gpx", "old.gpx"], dialog.Files.Select(f => f.Name));
        Assert.StartsWith("Current · ", dialog.Files[0].Details);
    }

    [Fact]
    public async Task Sending_copies_the_document_as_gpx_into_the_device_and_asks_before_replacing()
    {
        var device = DriveGpsDeviceFinder.Inspect(Garmin(), "GARMIN")!;
        var dialogs = new FakeDialogService();
        var store = new FakeDocumentStore();
        var vm = new MainViewModel(dialogs, store, new FakeSettingsStore(), NullLogger<MainViewModel>.Instance) { GpsDevices = new Finder(device) };
        dialogs.OpenPaths.Enqueue(@"C:\data\Aitzgorri.gpx");
        await vm.OpenAsync(DocumentFormat.Gpx);
        dialogs.ToolDialog = d =>
        {
            var gps = Assert.IsType<GpsDeviceViewModel>(d);
            Assert.Equal("Aitzgorri", gps.FileName);
            gps.FileName = "Ruta del domingo";
            return true;
        };

        await vm.SendToGpsAsync();

        var expected = Path.Combine(device.SendFolder, "Ruta del domingo.gpx");
        Assert.Equal([expected], store.SavedPaths);
        Assert.Empty(dialogs.Confirmations);

        File.WriteAllText(expected, "<gpx/>");
        dialogs.ConfirmAnswer = false;
        await vm.SendToGpsAsync();
        Assert.Single(dialogs.Confirmations);
        Assert.Single(store.SavedPaths); // not replaced
    }

    [Fact]
    public async Task Receiving_opens_the_checked_files()
    {
        var device = DriveGpsDeviceFinder.Inspect(Garmin(), "GARMIN")!;
        var file = Path.Combine(device.SendFolder, "Current", "Current.gpx");
        File.WriteAllText(file, "<gpx/>");
        var dialogs = new FakeDialogService();
        var vm = new MainViewModel(dialogs, new FakeDocumentStore(), new FakeSettingsStore(), NullLogger<MainViewModel>.Instance) { GpsDevices = new Finder(device) };
        dialogs.ToolDialog = d =>
        {
            ((GpsDeviceViewModel)d).Files[0].IsChecked = true;
            return true;
        };

        await vm.ReceiveFromGpsAsync();

        Assert.Equal("Current.gpx", Assert.Single(vm.Documents).FileName);
    }

    [Fact]
    public void Without_a_device_nothing_can_be_accepted_and_a_bad_name_is_refused()
    {
        var none = new GpsDeviceViewModel(new Finder(), sending: true, "a");
        Assert.True(none.HasNoDevice);
        Assert.False(none.CanAccept);

        var dialog = new GpsDeviceViewModel(new Finder(DriveGpsDeviceFinder.Inspect(Garmin())!), sending: true, "a:b");
        Assert.Null(dialog.Destination);
        dialog.FileName = "ruta.GPX";
        Assert.EndsWith("ruta.GPX", dialog.Destination);
    }

    private sealed class Finder(params GpsDevice[] devices) : IGpsDeviceFinder
    {
        public IReadOnlyList<GpsDevice> Find() => devices;
    }
}
