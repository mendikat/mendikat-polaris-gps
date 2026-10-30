using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Polaris.Gps.App.Services;

/// <summary>Brand of a GPS connected by USB; decides its folders.</summary>
public enum GpsDeviceKind
{
    Garmin,
    TwoNav,
}

/// <summary>A GPS connected as a USB drive (mass storage).</summary>
/// <param name="Name">"GPSMAP 66s (E:)": the model when the device says it, else the volume label.</param>
/// <param name="SendFolder">Where GPX files are copied so the GPS loads them.</param>
/// <param name="ReceiveFolders">Where the GPS keeps its tracks, routes and waypoints.</param>
public sealed record GpsDevice(string Name, string Root, GpsDeviceKind Kind, string SendFolder, IReadOnlyList<string> ReceiveFolders)
{
    public override string ToString() => Name;
}

/// <summary>Finds the GPS connected to the computer.</summary>
public interface IGpsDeviceFinder
{
    IReadOnlyList<GpsDevice> Find();
}

/// <summary>
/// GPS connected as USB drives: Garmin (folder <c>Garmin\GPX</c>, whose GPX files the device loads at start-up; it
/// records in <c>Current</c> and <c>Archive</c>) and TwoNav (<c>TwoNavData\Data</c>). Devices that connect as media
/// devices (MTP), as many recent watches do, are not drives and are not found.
/// </summary>
public sealed class DriveGpsDeviceFinder : IGpsDeviceFinder
{
    /// <summary>File extensions of the GPS files Polaris GPS can open.</summary>
    public static readonly string[] ReadableExtensions = [".gpx", ".trk", ".wpt", ".plt", ".kml", ".kmz", ".tcx"];

    public IReadOnlyList<GpsDevice> Find()
    {
        var devices = new List<GpsDevice>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.IsReady && drive.DriveType is DriveType.Removable or DriveType.Fixed
                    && Inspect(drive.RootDirectory.FullName, drive.VolumeLabel) is { } device)
                {
                    devices.Add(device);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A drive that cannot be read (card reader without card, locked volume) is simply not a GPS.
            }
        }

        return devices;
    }

    /// <summary>The GPS whose drive root is <paramref name="root"/>, or null when it has no Garmin or TwoNav folders.</summary>
    public static GpsDevice? Inspect(string root, string? volumeLabel = null)
    {
        var letter = Path.GetPathRoot(root)?.TrimEnd('\\') ?? root;
        var garmin = Path.Combine(root, "Garmin", "GPX");
        if (Directory.Exists(garmin))
        {
            var model = GarminModel(Path.Combine(root, "Garmin", "GarminDevice.xml")) ?? Label(volumeLabel, "Garmin");
            return new GpsDevice($"{model} ({letter})", root, GpsDeviceKind.Garmin, garmin,
                [garmin, Path.Combine(garmin, "Current"), Path.Combine(garmin, "Archive")]);
        }

        var twoNav = Path.Combine(root, "TwoNavData", "Data");
        if (Directory.Exists(twoNav))
        {
            return new GpsDevice($"{Label(volumeLabel, "TwoNav")} ({letter})", root, GpsDeviceKind.TwoNav, twoNav, [twoNav]);
        }

        return null;
    }

    /// <summary>The readable GPS files of the device, newest first.</summary>
    public static IReadOnlyList<FileInfo> Files(GpsDevice device) =>
        device.ReceiveFolders
            .Where(Directory.Exists)
            .SelectMany(folder => new DirectoryInfo(folder).EnumerateFiles())
            .Where(f => ReadableExtensions.Contains(f.Extension, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .ToList();

    /// <summary>The model in <c>GarminDevice.xml</c> (Device › Model › Description), or null.</summary>
    private static string? GarminModel(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var description = XDocument.Load(path).Descendants().FirstOrDefault(e => e.Name.LocalName == "Model")?
                .Elements().FirstOrDefault(e => e.Name.LocalName == "Description")?.Value.Trim();
            return string.IsNullOrEmpty(description) ? null : description;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            return null;
        }
    }

    private static string Label(string? volumeLabel, string fallback) => string.IsNullOrWhiteSpace(volumeLabel) ? fallback : volumeLabel.Trim();
}
