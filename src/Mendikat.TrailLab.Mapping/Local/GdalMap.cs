using BruTile;
using BruTile.Predefined;
using Mendikat.TrailLab.Domain.Geo;
using OSGeo.GDAL;

namespace Mendikat.TrailLab.Mapping.Local;

/// <summary>
/// A map image read with GDAL (GeoTIFF, ECW, ECWP), drawn as Web Mercator tiles on demand. Each tile reads only the
/// window of the raster it covers, at the resolution it needs (GDAL then uses the ECW pyramid or the GeoTIFF
/// overviews), and resamples it through a 17 × 17 grid of exact transformations, like the calibrated images.
/// RGB(A), palette and grey images are supported; pixels equal to the NoData value are transparent.
/// </summary>
public sealed class GdalMap : ILocalMap, IDisposable
{
    private const int TileSize = LocalMapTileSource.TileSize;
    private const int GridStep = 16;

    /// <summary>Largest read buffer side: about twice the tile, enough for a sharp result.</summary>
    private const int MaxBuffer = 2 * TileSize;

    private readonly int[] _bands;
    private readonly int _alphaBand;
    private readonly byte[][]? _palette;
    private readonly double?[] _noData;

    public GdalMap(GdalRaster raster)
    {
        Raster = raster;
        var dataset = raster.Dataset;
        var interpretations = Enumerable.Range(1, dataset.RasterCount).Select(i =>
        {
            using var band = dataset.GetRasterBand(i);
            return band.GetRasterColorInterpretation();
        }).ToList();
        int Find(ColorInterp interp, int fallback) => interpretations.IndexOf(interp) is var i and >= 0 ? i + 1 : fallback;

        if (dataset.RasterCount >= 3)
        {
            _bands = [Find(ColorInterp.GCI_RedBand, 1), Find(ColorInterp.GCI_GreenBand, 2), Find(ColorInterp.GCI_BlueBand, 3)];
            _alphaBand = Find(ColorInterp.GCI_AlphaBand, 0);
        }
        else
        {
            _bands = [1];
            _alphaBand = dataset.RasterCount == 2 ? Find(ColorInterp.GCI_AlphaBand, 0) : 0;
            using var band = dataset.GetRasterBand(1);
            if (band.GetRasterColorTable() is { } table)
            {
                _palette = Enumerable.Range(0, Math.Min(256, table.GetCount())).Select(i =>
                {
                    var entry = table.GetColorEntry(i);
                    return new[] { (byte)entry.c1, (byte)entry.c2, (byte)entry.c3, (byte)entry.c4 };
                }).ToArray();
            }
        }

        _noData = _bands.Select(b =>
        {
            using var band = dataset.GetRasterBand(b);
            band.GetNoDataValue(out var value, out var has);
            return has != 0 ? value : (double?)null;
        }).ToArray();
    }

    public GdalRaster Raster { get; }

    public GeoBounds Bounds => Raster.Bounds;

    public int BandCount => Raster.Dataset.RasterCount;

    public bool HasPalette => _palette is not null;

    public ILocalTileSource CreateTileSource(string name) => new GdalTileSource(this, name);

    /// <summary>One tile as PNG (Web Mercator extent), or null if the raster does not cover it.</summary>
    public byte[]? Render(double minX, double minY, double maxX, double maxY)
    {
        const int Nodes = (TileSize / GridStep) + 1;
        var gx = new double[Nodes * Nodes];
        var gy = new double[Nodes * Nodes];
        for (var j = 0; j < Nodes; j++)
        {
            for (var i = 0; i < Nodes; i++)
            {
                gx[(j * Nodes) + i] = minX + ((maxX - minX) * i * GridStep / TileSize);
                gy[(j * Nodes) + i] = maxY - ((maxY - minY) * j * GridStep / TileSize);
            }
        }

        lock (Raster.Sync)
        {
            if (Raster.IsDisposed)
            {
                return null;
            }

            Raster.PixelsFromWebMercator(gx, gy);
            var (x0, y0, x1, y1) = Window(gx, gy);
            if (x1 <= x0 || y1 <= y0)
            {
                return null;
            }

            var (ww, wh) = (x1 - x0, y1 - y0);
            var scale = Math.Min(1.0, (double)MaxBuffer / Math.Max(ww, wh));
            var bw = Math.Max(1, (int)Math.Round(ww * scale));
            var bh = Math.Max(1, (int)Math.Round(wh * scale));
            var buffers = _bands.Select(b => Read(b, x0, y0, ww, wh, bw, bh)).ToArray();
            var alpha = _alphaBand > 0 ? Read(_alphaBand, x0, y0, ww, wh, bw, bh) : null;
            return Compose(gx, gy, x0, y0, ww, wh, bw, bh, buffers, alpha);
        }
    }

    public void Dispose() => Raster.Dispose();

    /// <summary>Raster window (whole pixels) covered by the valid grid nodes, clipped to the raster.</summary>
    private (int X0, int Y0, int X1, int Y1) Window(double[] gx, double[] gy)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        for (var k = 0; k < gx.Length; k++)
        {
            if (double.IsFinite(gx[k]) && double.IsFinite(gy[k]))
            {
                (minX, maxX) = (Math.Min(minX, gx[k]), Math.Max(maxX, gx[k]));
                (minY, maxY) = (Math.Min(minY, gy[k]), Math.Max(maxY, gy[k]));
            }
        }

        if (minX > maxX)
        {
            return (0, 0, 0, 0);
        }

        return (
            Math.Clamp((int)Math.Floor(minX), 0, Raster.Width),
            Math.Clamp((int)Math.Floor(minY), 0, Raster.Height),
            Math.Clamp((int)Math.Ceiling(maxX) + 1, 0, Raster.Width),
            Math.Clamp((int)Math.Ceiling(maxY) + 1, 0, Raster.Height));
    }

    private byte[] Read(int bandIndex, int x0, int y0, int ww, int wh, int bw, int bh)
    {
        var buffer = new byte[bw * bh];
        using var band = Raster.Dataset.GetRasterBand(bandIndex);
        band.ReadRaster(x0, y0, ww, wh, buffer, bw, bh, 0, 0);
        return buffer;
    }

    private byte[]? Compose(double[] gx, double[] gy, int x0, int y0, int ww, int wh, int bw, int bh, byte[][] buffers, byte[]? alpha)
    {
        const int Nodes = (TileSize / GridStep) + 1;
        var rgba = new byte[TileSize * TileSize * 4];
        var any = false;
        for (var y = 0; y < TileSize; y++)
        {
            var j = y / GridStep;
            var fy = (y + 0.5 - (j * GridStep)) / GridStep;
            for (var x = 0; x < TileSize; x++)
            {
                var i = x / GridStep;
                var fx = (x + 0.5 - (i * GridStep)) / GridStep;
                var sx = Lerp(gx, Nodes, i, j, fx, fy);
                var sy = Lerp(gy, Nodes, i, j, fx, fy);
                if (!(sx >= x0 && sy >= y0 && sx < x0 + ww && sy < y0 + wh))
                {
                    continue;
                }

                var bx = Math.Min(bw - 1, (int)((sx - x0) * bw / ww));
                var by = Math.Min(bh - 1, (int)((sy - y0) * bh / wh));
                var s = (by * bw) + bx;
                var d = ((y * TileSize) + x) * 4;
                if (!Pixel(buffers, s, rgba.AsSpan(d, 4)))
                {
                    continue;
                }

                if (alpha is not null)
                {
                    rgba[d + 3] = alpha[s];
                }

                any |= rgba[d + 3] != 0;
            }
        }

        return any ? PngEncoder.Encode(TileSize, TileSize, rgba) : null;
    }

    /// <summary>Writes RGBA; false if the pixel is NoData (left transparent).</summary>
    private bool Pixel(byte[][] buffers, int s, Span<byte> rgba)
    {
        if (buffers.Length >= 3)
        {
            if (_noData[0] is { } n0 && _noData[1] is { } n1 && _noData[2] is { } n2
                && buffers[0][s] == n0 && buffers[1][s] == n1 && buffers[2][s] == n2)
            {
                return false;
            }

            (rgba[0], rgba[1], rgba[2], rgba[3]) = (buffers[0][s], buffers[1][s], buffers[2][s], 255);
            return true;
        }

        var value = buffers[0][s];
        if (_noData[0] is { } nd && value == nd)
        {
            return false;
        }

        if (_palette is not null)
        {
            var entry = value < _palette.Length ? _palette[value] : [0, 0, 0, 0];
            (rgba[0], rgba[1], rgba[2], rgba[3]) = (entry[0], entry[1], entry[2], entry[3]);
            return true;
        }

        (rgba[0], rgba[1], rgba[2], rgba[3]) = (value, value, value, 255);
        return true;
    }

    private static double Lerp(double[] grid, int nodes, int i, int j, double fx, double fy)
    {
        var top = grid[(j * nodes) + i] + ((grid[(j * nodes) + i + 1] - grid[(j * nodes) + i]) * fx);
        var bottom = grid[((j + 1) * nodes) + i] + ((grid[((j + 1) * nodes) + i + 1] - grid[((j + 1) * nodes) + i]) * fx);
        return top + ((bottom - top) * fy);
    }

    private sealed class GdalTileSource(GdalMap map, string name) : ILocalTileSource
    {
        public ITileSchema Schema { get; } = new GlobalSphericalMercator(0, 21, name);

        public string Name => name;

        public Attribution Attribution { get; } = new();

        public Task<byte[]?> GetTileAsync(TileInfo tileInfo)
        {
            var e = tileInfo.Extent;
            var b = map.Bounds;
            var (minX, minY) = Coordinates.WebMercator.ToWorld(new GeoPosition(b.MinLatitude, b.MinLongitude));
            var (maxX, maxY) = Coordinates.WebMercator.ToWorld(new GeoPosition(b.MaxLatitude, b.MaxLongitude));
            if (e.MaxX < minX || e.MinX > maxX || e.MaxY < minY || e.MinY > maxY)
            {
                return Task.FromResult<byte[]?>(null);
            }

            return Task.Run(() => map.Render(e.MinX, e.MinY, e.MaxX, e.MaxY));
        }
    }
}

/// <summary>
/// A DEM read with GDAL (GeoTIFF with one numeric band): elevations interpolated bilinearly between the four nearest
/// pixel centres, reading only those pixels. NoData, positions outside the raster and failed transformations give
/// no data (never 0 m).
/// </summary>
public sealed partial class GdalDem : ILocalDem, IDisposable
{
    private readonly double? _noData;

    public GdalDem(GdalRaster raster)
    {
        Raster = raster;
        using var band = raster.Dataset.GetRasterBand(1);
        band.GetNoDataValue(out var value, out var has);
        _noData = has != 0 ? value : null;
        var unit = band.GetUnitType();
        var wkt = raster.Dataset.GetProjectionRef();
        var vertical = VerticalCrs().Match(wkt) is { Success: true } m ? m.Groups[1].Value : null;
        VerticalDatum = (vertical is null
            ? "Referencia vertical no indicada en el archivo (desconocida)"
            : $"Referencia vertical: {vertical}")
            + (string.IsNullOrWhiteSpace(unit) ? string.Empty : $"; unidades {unit}")
            + $"; {raster.CrsDescription}.";
    }

    public GdalRaster Raster { get; }

    public string Name => GdalRaster.DisplayName(Raster.Source);

    public string VerticalDatum { get; }

    public string Attribution => $"MDT local «{Name}»";

    public GeoBounds Bounds => Raster.Bounds;

    public int Columns => Raster.Width;

    public int Rows => Raster.Height;

    public string CrsDescription => Raster.CrsDescription;

    public Task<IReadOnlyList<double?>> GetElevationsAsync(
        IReadOnlyList<GeoPosition> positions, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var result = new double?[positions.Count];
        for (var i = 0; i < positions.Count; i++)
        {
            if (i % 1024 == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report((double)i / positions.Count);
            }

            result[i] = ElevationAt(positions[i]);
        }

        progress?.Report(1);
        return Task.FromResult<IReadOnlyList<double?>>(result);
    }

    public double? ElevationAt(GeoPosition position)
    {
        if (position.Latitude < Bounds.MinLatitude - 0.01 || position.Latitude > Bounds.MaxLatitude + 0.01
            || position.Longitude < Bounds.MinLongitude - 0.01 || position.Longitude > Bounds.MaxLongitude + 0.01)
        {
            return null;
        }

        lock (Raster.Sync)
        {
            if (Raster.IsDisposed)
            {
                return null;
            }

            double[] x = [position.Longitude], y = [position.Latitude];
            Raster.PixelsFromWgs84(x, y);

            // Pixel values belong to pixel centres (+0.5).
            var fx = x[0] - 0.5;
            var fy = y[0] - 0.5;
            if (!(fx >= -0.5 && fy >= -0.5 && fx <= Raster.Width - 0.5 && fy <= Raster.Height - 0.5))
            {
                return null;
            }

            var c = Math.Clamp((int)Math.Floor(fx), 0, Math.Max(0, Raster.Width - 2));
            var r = Math.Clamp((int)Math.Floor(fy), 0, Math.Max(0, Raster.Height - 2));
            var w = Math.Min(2, Raster.Width - c);
            var h = Math.Min(2, Raster.Height - r);
            var values = new double[w * h];
            using var band = Raster.Dataset.GetRasterBand(1);
            if (band.ReadRaster(c, r, w, h, values, w, h, 0, 0) != CPLErr.CE_None)
            {
                return null;
            }

            var tx = Math.Clamp(fx - c, 0, 1);
            var ty = Math.Clamp(fy - r, 0, 1);
            // A pixel without data spoils the result only if it really weighs in it: a position on a pixel centre or
            // edge comes back a hair off after the coordinate conversions (1e-4 of a pixel is a few millimetres).
            double sum = 0, total = 0;
            for (var j = 0; j < h; j++)
            {
                for (var i = 0; i < w; i++)
                {
                    var weight = (w == 1 ? 1 : (i == 0 ? 1 - tx : tx)) * (h == 1 ? 1 : (j == 0 ? 1 - ty : ty));
                    if (weight < 1e-4)
                    {
                        continue;
                    }

                    var v = values[(j * w) + i];
                    if (!double.IsFinite(v) || (_noData is { } nd && v == nd))
                    {
                        return null;
                    }

                    sum += v * weight;
                    total += weight;
                }
            }

            return total > 0 ? sum / total : null;
        }
    }

    public void Dispose() => Raster.Dispose();

    [System.Text.RegularExpressions.GeneratedRegex(@"VERT(?:_CS|CRS)\s*\[\s*""([^""]+)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex VerticalCrs();
}
