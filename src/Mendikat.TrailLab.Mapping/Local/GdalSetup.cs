using System.Runtime.InteropServices;
using OSGeo.GDAL;
using OSGeo.OSR;

namespace Mendikat.TrailLab.Mapping.Local;

/// <summary>
/// Configures GDAL once (GeoTIFF, ECW): the native libraries live in "gdal\x64" next to the application, with the
/// ECW plugin in "gdal\x64\plugins", GDAL data in "gdal\data" and the PROJ database in "gdal\share". Errors are kept
/// quiet (no console) and read from <see cref="Gdal.GetLastErrorMsg"/> when an operation fails.
/// </summary>
public static partial class GdalSetup
{
    private const uint LoadLibrarySearchDefaultDirs = 0x00001000;
    private static readonly Lazy<string?> Error = new(Configure, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Null when GDAL is usable; otherwise why not.</summary>
    public static string? Problem => Error.Value;

    /// <summary>True if the ECW plugin (and the Hexagon SDK library) loaded.</summary>
    public static bool HasEcw => Problem is null && Gdal.GetDriverByName("ECW") is not null;

    /// <exception cref="LocalMapException">GDAL could not be loaded.</exception>
    public static void EnsureConfigured()
    {
        if (Problem is { } problem)
        {
            throw new LocalMapException(LocalMapError.GdalUnavailable, problem);
        }
    }

    private static string? Configure()
    {
        try
        {
            var gdalPath = Path.Combine(AppContext.BaseDirectory, "gdal");
            var nativePath = Path.Combine(gdalPath, "x64");
            if (!File.Exists(Path.Combine(nativePath, "gdal_wrap.dll")))
            {
                return $"no se encuentra {Path.Combine("gdal", "x64", "gdal_wrap.dll")}";
            }

            // Dependencies of gdal_wrap.dll and of the plugins are searched in these folders.
            SetDefaultDllDirectories(LoadLibrarySearchDefaultDirs);
            AddDllDirectory(nativePath);
            AddDllDirectory(Path.Combine(nativePath, "plugins"));

            var data = Path.Combine(gdalPath, "data");
            var proj = Path.Combine(gdalPath, "share");
            Gdal.SetConfigOption("GDAL_DATA", data);
            Gdal.SetConfigOption("GDAL_DRIVER_PATH", Path.Combine(nativePath, "plugins"));
            Gdal.SetConfigOption("GEOTIFF_CSV", data);
            Gdal.SetConfigOption("PROJ_LIB", proj);
            Gdal.SetConfigOption("GDAL_CURL_CA_BUNDLE", Path.Combine(gdalPath, "curl-ca-bundle.crt"));
            Osr.SetPROJSearchPaths([proj]);
            Gdal.PushErrorHandler("CPLQuietErrorHandler");
            Gdal.AllRegister();
            return null;
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException or TypeInitializationException or EntryPointNotFoundException or ApplicationException)
        {
            return ex.Message;
        }
    }

    [LibraryImport("kernel32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetDefaultDllDirectories(uint directoryFlags);

    [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint AddDllDirectory(string path);
}
