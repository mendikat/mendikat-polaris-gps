using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Mapping.Coordinates;
using OSGeo.GDAL;
using OSGeo.OSR;

namespace Mendikat.TrailLab.Mapping.Local;

/// <summary>
/// A raster opened with GDAL (GeoTIFF, ECW, ECWP stream): its georeferencing in any coordinate system that PROJ knows,
/// and transformations from WGS84 and Web Mercator to its pixels. A raster georeferenced only by control points (GCP)
/// is warped on the fly to a north-up grid. GDAL datasets are not thread-safe: every read goes through <see cref="Sync"/>.
/// </summary>
public sealed class GdalRaster : IDisposable
{
    private readonly double[] _inverse = new double[6];
    private readonly CoordinateTransformation _fromWgs84;
    private readonly CoordinateTransformation _fromWebMercator;
    private readonly CoordinateTransformation _toWgs84;
    private bool _disposed;

    private GdalRaster(string source, Dataset dataset, string driver, SpatialReference srs)
    {
        Source = source;
        Dataset = dataset;
        Driver = driver;
        Width = dataset.RasterXSize;
        Height = dataset.RasterYSize;
        dataset.GetGeoTransform(GeoTransform);
        Gdal.InvGeoTransform(GeoTransform, _inverse);
        srs.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);
        CrsDescription = srs.GetName() is { Length: > 0 } name ? name : "?";
        _fromWgs84 = new CoordinateTransformation(Srs(4326), srs);
        _fromWebMercator = new CoordinateTransformation(Srs(3857), srs);
        _toWgs84 = new CoordinateTransformation(srs, Srs(4326));
        Bounds = ComputeBounds();
    }

    /// <summary>File path or ECWP URL.</summary>
    public string Source { get; }

    /// <summary>GDAL driver, e.g. "GTiff" or "ECW".</summary>
    public string Driver { get; }

    public int Width { get; }

    public int Height { get; }

    public string CrsDescription { get; }

    /// <summary>WGS84 extent of the raster.</summary>
    public GeoBounds Bounds { get; }

    /// <summary>Lock for every access to <see cref="Dataset"/>.</summary>
    public object Sync { get; } = new();

    internal Dataset Dataset { get; }

    /// <summary>GDAL geotransform: pixel/line (corner) → coordinates.</summary>
    internal double[] GeoTransform { get; } = new double[6];

    public bool IsDisposed => _disposed;

    /// <summary>Opens a raster read-only. Call off the UI thread.</summary>
    /// <exception cref="LocalMapException">Cannot be opened or is not georeferenced.</exception>
    public static GdalRaster Open(string source)
    {
        GdalSetup.EnsureConfigured();
        var name = DisplayName(source);
        var isEcw = IsEcw(source);
        if (isEcw && !GdalSetup.HasEcw)
        {
            throw new LocalMapException(LocalMapError.EcwUnavailable, name);
        }

        Dataset? dataset;
        try
        {
            dataset = Gdal.Open(source, Access.GA_ReadOnly);
        }
        catch (ApplicationException ex)
        {
            throw new LocalMapException(LocalMapError.ReadFailed, $"{name}: {ex.Message}", innerException: ex);
        }

        if (dataset is null)
        {
            var message = Gdal.GetLastErrorMsg();
            throw new LocalMapException(LocalMapError.ReadFailed, string.IsNullOrWhiteSpace(message) ? name : $"{name}: {message}");
        }

        try
        {
            if (dataset.RasterCount == 0)
            {
                throw new LocalMapException(LocalMapError.ReadFailed, $"{name}: sin bandas");
            }

            var wkt = dataset.GetProjectionRef();
            if (string.IsNullOrWhiteSpace(wkt) && dataset.GetGCPCount() > 0 && !string.IsNullOrWhiteSpace(dataset.GetGCPProjection()))
            {
                // Georeferenced by control points only: a warped virtual dataset gives a north-up grid.
                var warped = Gdal.AutoCreateWarpedVRT(dataset, dataset.GetGCPProjection(), null, ResampleAlg.GRA_Bilinear, 0.125);
                if (warped is not null)
                {
                    dataset = warped;
                    wkt = dataset.GetProjectionRef();
                }
            }

            var transform = new double[6];
            dataset.GetGeoTransform(transform);
            var identity = transform is [0, 1, 0, 0, 0, 1];
            if (identity)
            {
                throw new LocalMapException(LocalMapError.NotGeoreferenced, name);
            }

            // Placed by a world file (.tfw, .jgw, .pgw…), which has no coordinate system: it comes from the .prj.
            var srs = string.IsNullOrWhiteSpace(wkt) ? SrsFromPrj(source, name) : new SpatialReference(wkt);
            return new GdalRaster(source, dataset, dataset.GetDriver().ShortName, srs);
        }
        catch
        {
            dataset.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The coordinate system of the .prj next to an image (ESRI or OGC WKT, the classic ArcInfo "Projection UTM / Zone"
    /// text, or "EPSG:n"), read by GDAL. Without a .prj the image is not opened: guessing could place it far away.
    /// </summary>
    private static SpatialReference SrsFromPrj(string source, string name)
    {
        if (LocalMapLoader.IsEcwpUrl(source) || LocalFiles.FindSidecar(source, ".prj") is not { } prj)
        {
            throw new LocalMapException(LocalMapError.MissingProjection, Path.GetFileNameWithoutExtension(name).Trim() + ".prj");
        }

        var text = File.ReadAllText(prj).Trim();
        var srs = new SpatialReference(string.Empty);
        try
        {
            var lines = text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (srs.ImportFromESRI(lines) == 0 && !string.IsNullOrEmpty(Export(srs)))
            {
                return srs;
            }
        }
        catch (ApplicationException)
        {
            // not in an ESRI form: try any other definition GDAL knows
        }

        try
        {
            if (srs.SetFromUserInput(text) == 0 && !string.IsNullOrEmpty(Export(srs)))
            {
                return srs;
            }
        }
        catch (ApplicationException)
        {
            // reported below
        }

        srs.Dispose();
        throw new LocalMapException(LocalMapError.UnsupportedCrs, $"{Path.GetFileName(prj)}: {(text.Length > 60 ? text[..60] + "…" : text)}");
    }

    private static string Export(SpatialReference srs)
    {
        srs.ExportToWkt(out var wkt, null);
        return wkt;
    }

    public static bool IsEcw(string source) =>
        source.StartsWith("ecwp://", StringComparison.OrdinalIgnoreCase)
        || source.StartsWith("ecwps://", StringComparison.OrdinalIgnoreCase)
        || source.EndsWith(".ecw", StringComparison.OrdinalIgnoreCase);

    /// <summary>File name, or the last part of an ECWP URL.</summary>
    public static string DisplayName(string source)
    {
        var trimmed = source.TrimEnd('/', '\\');
        return trimmed[(trimmed.LastIndexOfAny(['/', '\\']) + 1)..];
    }

    /// <summary>Pixel (column, row; 0,0 = top-left corner) of WGS84 positions; NaN where the transformation fails.</summary>
    public void PixelsFromWgs84(double[] lon, double[] lat) => ToPixels(_fromWgs84, lon, lat);

    /// <summary>Pixel of Web Mercator points (in place); NaN where the transformation fails.</summary>
    public void PixelsFromWebMercator(double[] x, double[] y) => ToPixels(_fromWebMercator, x, y);

    public void Dispose()
    {
        lock (Sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _fromWgs84.Dispose();
            _fromWebMercator.Dispose();
            _toWgs84.Dispose();
            Dataset.Dispose();
        }
    }

    private void ToPixels(CoordinateTransformation transformation, double[] x, double[] y)
    {
        var z = new double[x.Length];
        try
        {
            transformation.TransformPoints(x.Length, x, y, z);
        }
        catch (ApplicationException)
        {
            Array.Fill(x, double.NaN);
            Array.Fill(y, double.NaN);
            return;
        }

        for (var i = 0; i < x.Length; i++)
        {
            if (double.IsInfinity(x[i]) || double.IsInfinity(y[i]))
            {
                (x[i], y[i]) = (double.NaN, double.NaN);
                continue;
            }

            var px = _inverse[0] + (_inverse[1] * x[i]) + (_inverse[2] * y[i]);
            var py = _inverse[3] + (_inverse[4] * x[i]) + (_inverse[5] * y[i]);
            (x[i], y[i]) = (px, py);
        }
    }

    private GeoBounds ComputeBounds()
    {
        const int Steps = 16;
        var xs = new List<double>();
        var ys = new List<double>();
        for (var i = 0; i <= Steps; i++)
        {
            var t = (double)i / Steps;
            foreach (var (px, py) in new[] { (t * Width, 0.0), (t * Width, (double)Height), (0.0, t * Height), ((double)Width, t * Height) })
            {
                xs.Add(GeoTransform[0] + (GeoTransform[1] * px) + (GeoTransform[2] * py));
                ys.Add(GeoTransform[3] + (GeoTransform[4] * px) + (GeoTransform[5] * py));
            }
        }

        var lon = xs.ToArray();
        var lat = ys.ToArray();
        _toWgs84.TransformPoints(lon.Length, lon, lat, new double[lon.Length]);
        var positions = lon.Zip(lat)
            .Where(p => double.IsFinite(p.First) && double.IsFinite(p.Second) && Math.Abs(p.Second) <= 90 && Math.Abs(p.First) <= 180)
            .Select(p => new GeoPosition(p.Second, p.First))
            .ToList();
        return GeoBounds.FromPositions(positions)
            ?? throw new LocalMapException(LocalMapError.NotGeoreferenced, DisplayName(Source));
    }

    private static SpatialReference Srs(int epsg)
    {
        var srs = new SpatialReference(string.Empty);
        srs.ImportFromEPSG(epsg);
        srs.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);
        return srs;
    }
}

/// <summary>Opens GeoTIFF, ECW and ECWP sources and tells a map (imagery) from a DEM (one numeric band).</summary>
public static class GdalLoader
{
    /// <summary>Formats read with GDAL: GeoTIFF, ECW, and images placed by a world file and a .prj.</summary>
    public static IReadOnlyList<string> Extensions { get; } = [".tif", ".tiff", ".cog", ".ecw", ".jpg", ".jpeg", ".png", ".gif", ".bmp"];

    /// <exception cref="LocalMapException">Cannot be opened.</exception>
    public static LocalMapContent Load(string source, CancellationToken cancellationToken)
    {
        var raster = GdalRaster.Open(source);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var band = raster.Dataset.GetRasterBand(1);
            var isDem = raster.Dataset.RasterCount == 1
                && band.DataType is not (DataType.GDT_Byte or DataType.GDT_Unknown)
                && band.GetRasterColorTable() is null;
            return isDem
                ? new LocalDem(source, new GdalDem(raster))
                : new LocalGdalMap(source, new GdalMap(raster));
        }
        catch
        {
            raster.Dispose();
            throw;
        }
    }
}
