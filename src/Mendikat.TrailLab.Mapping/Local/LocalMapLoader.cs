using BruTile;
using Mendikat.TrailLab.Domain.Geo;

namespace Mendikat.TrailLab.Mapping.Local;

/// <summary>A loaded local source: a map to draw or a DEM to query. Dispose it when it is removed.</summary>
public abstract record LocalMapContent(string Path) : IDisposable
{
    /// <summary>File name, or the last part of an ECWP URL.</summary>
    public string FileName => GdalRaster.DisplayName(Path);

    public virtual void Dispose()
    {
    }
}

/// <summary>A calibrated image (.map, .imp) and its decoded pixels.</summary>
public sealed record LocalRasterMap(string Path, GeoreferencedMap Map, RasterImage Image) : LocalMapContent(Path), ILocalMap
{
    public GeoBounds Bounds => Map.Bounds;

    public ILocalTileSource CreateTileSource(string name) => new LocalMapTileSource(Map, Image, name);
}

/// <summary>A georeferenced image read with GDAL: GeoTIFF, ECW or an ECWP stream.</summary>
public sealed record LocalGdalMap(string Path, GdalMap Map) : LocalMapContent(Path), ILocalMap
{
    public GeoBounds Bounds => Map.Bounds;

    public ILocalTileSource CreateTileSource(string name) => Map.CreateTileSource(name);

    public override void Dispose() => Map.Dispose();
}

/// <summary>A digital elevation model (.asc, .hgt, GeoTIFF).</summary>
public sealed record LocalDem(string Path, ILocalDem Dem) : LocalMapContent(Path)
{
    public override void Dispose() => (Dem as IDisposable)?.Dispose();
}

/// <summary>
/// Opens local cartography: OziExplorer .map and CompeGPS .imp calibrations with their image and ASCII grid (.asc)
/// and SRTM (.hgt) DEMs with own readers; GeoTIFF (maps and DEMs), ECW and ECWP streams with GDAL. Format by extension,
/// then validated by content. Call off the UI thread.
/// </summary>
public static class LocalMapLoader
{
    /// <summary>Images with more pixels are decoded at a reduced size (4 bytes per pixel: about 200 MB).</summary>
    public const long MaxDecodedPixels = 50_000_000;

    public static IReadOnlyList<string> Extensions { get; } = [".map", ".imp", ".asc", ".hgt", .. GdalLoader.Extensions, .. LocalFiles.WorldFiles.Keys];

    /// <summary>True for an ECWP stream address (ecwp:// or ecwps://).</summary>
    public static bool IsEcwpUrl(string source) =>
        source.StartsWith("ecwp://", StringComparison.OrdinalIgnoreCase) || source.StartsWith("ecwps://", StringComparison.OrdinalIgnoreCase);

    /// <exception cref="LocalMapException">The file cannot be used; nothing is loaded.</exception>
    /// <exception cref="OperationCanceledException">Cancelled.</exception>
    public static LocalMapContent Load(string path, IRasterImageDecoder decoder, CancellationToken cancellationToken = default)
    {
        if (IsEcwpUrl(path))
        {
            return GdalLoader.Load(path, cancellationToken);
        }

        var info = new FileInfo(path);
        if (!info.Exists)
        {
            throw new LocalMapException(LocalMapError.FileNotFound, info.Name);
        }

        // A world file chosen instead of its image (mapa.tfw → mapa.tif).
        if (LocalFiles.WorldFiles.ContainsKey(info.Extension))
        {
            var image = LocalFiles.ImageOfWorldFile(path) ?? throw new LocalMapException(LocalMapError.ImageMissing, Path.GetFileNameWithoutExtension(path) + string.Join(" / ", LocalFiles.WorldFiles[info.Extension]));
            return GdalLoader.Load(image, cancellationToken);
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
                ".tif" or ".tiff" or ".cog" or ".ecw" or ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => GdalLoader.Load(path, cancellationToken),
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
