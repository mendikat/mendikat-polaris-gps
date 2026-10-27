namespace Mendikat.TrailLab.Mapping.Local;

/// <summary>A loaded local file: a calibrated image to draw, or a DEM to query.</summary>
public abstract record LocalMapContent(string Path)
{
    public string FileName => System.IO.Path.GetFileName(Path);
}

/// <summary>A calibrated image (.map, .imp) and its decoded pixels.</summary>
public sealed record LocalRasterMap(string Path, GeoreferencedMap Map, RasterImage Image) : LocalMapContent(Path);

/// <summary>A digital elevation model (.asc, .hgt).</summary>
public sealed record LocalDem(string Path, ElevationGrid Grid) : LocalMapContent(Path);

/// <summary>
/// Opens local cartography (D7: only the simple formats, with own readers): OziExplorer .map and CompeGPS .imp
/// calibrations with their image, and ASCII grid (.asc) and SRTM (.hgt) DEMs. Format by extension, then validated by
/// content: each reader checks its signature or structure (LMAP-02). Call off the UI thread.
/// </summary>
public static class LocalMapLoader
{
    /// <summary>Images with more pixels are decoded at a reduced size (4 bytes per pixel: about 200 MB).</summary>
    public const long MaxDecodedPixels = 50_000_000;

    public static IReadOnlyList<string> Extensions { get; } = [".map", ".imp", ".asc", ".hgt"];

    /// <exception cref="LocalMapException">The file cannot be used; nothing is loaded.</exception>
    /// <exception cref="OperationCanceledException">Cancelled.</exception>
    public static LocalMapContent Load(string path, IRasterImageDecoder decoder, CancellationToken cancellationToken = default)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            throw new LocalMapException(LocalMapError.FileNotFound, info.Name);
        }

        if (info.Length == 0)
        {
            throw new LocalMapException(LocalMapError.EmptyFile, info.Name);
        }

        try
        {
            return info.Extension.ToLowerInvariant() switch
            {
                ".map" => LoadRaster(path, OziMapReader.Read(path), decoder, cancellationToken),
                ".imp" => LoadRaster(path, CompeImpReader.Read(path), decoder, cancellationToken),
                ".hgt" => new LocalDem(path, HgtReader.Read(path, cancellationToken)),
                ".asc" => new LocalDem(path, AscReader.Read(path, cancellationToken)),
                var other => throw new LocalMapException(LocalMapError.UnsupportedFormat, other.Length == 0 ? info.Name : other),
            };
        }
        catch (UnsupportedCrsException ex)
        {
            throw new LocalMapException(LocalMapError.UnsupportedCrs, ex.Detail, innerException: ex);
        }
    }

    private static LocalRasterMap LoadRaster(string path, MapCalibration calibration, IRasterImageDecoder decoder, CancellationToken cancellationToken)
    {
        // The image's own size wins over the one written in the calibration (the image may have been replaced).
        var (width, height) = decoder.ReadSize(calibration.ImagePath);
        var map = GeoreferencedMap.Create(calibration, width, height);
        cancellationToken.ThrowIfCancellationRequested();
        var image = decoder.Decode(calibration.ImagePath, MaxDecodedPixels, cancellationToken);
        return new LocalRasterMap(path, map, image);
    }
}
