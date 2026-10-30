using BruTile;
using BruTile.Predefined;
using Polaris.Gps.Mapping.Coordinates;

namespace Polaris.Gps.Mapping.Local;

/// <summary>
/// Draws a calibrated image as Web Mercator tiles on demand (LMAP-03, LMAP-06, LMAP-09): each 256 × 256 tile is
/// resampled from the decoded image through Web Mercator → WGS84 → map datum and projection → image pixel. The exact
/// transformation is computed on a 17 × 17 grid per tile and interpolated in between (error far below a pixel).
/// Pixels outside the image are transparent. Tiles are made on background threads.
/// </summary>
public sealed class LocalMapTileSource : ILocalTileSource
{
    public const int TileSize = 256;

    /// <summary>Exact transformation every this many pixels of the tile.</summary>
    private const int GridStep = 16;

    private readonly GeoreferencedMap _map;
    private readonly RasterImage _image;
    private readonly (double MinX, double MinY, double MaxX, double MaxY) _worldBounds;

    public LocalMapTileSource(GeoreferencedMap map, RasterImage image, string name)
    {
        _map = map;
        _image = image;
        Name = name;
        Schema = new GlobalSphericalMercator(0, 21, name);
        var (minX, minY) = WebMercator.ToWorld(new(map.Bounds.MinLatitude, map.Bounds.MinLongitude));
        var (maxX, maxY) = WebMercator.ToWorld(new(map.Bounds.MaxLatitude, map.Bounds.MaxLongitude));
        _worldBounds = (minX, minY, maxX, maxY);
    }

    public ITileSchema Schema { get; }

    public string Name { get; }

    public Attribution Attribution { get; } = new();

    public Task<byte[]?> GetTileAsync(TileInfo tileInfo)
    {
        var e = tileInfo.Extent;
        if (e.MaxX < _worldBounds.MinX || e.MinX > _worldBounds.MaxX || e.MaxY < _worldBounds.MinY || e.MinY > _worldBounds.MaxY)
        {
            return Task.FromResult<byte[]?>(null);
        }

        return Task.Run(() => Render(e.MinX, e.MinY, e.MaxX, e.MaxY));
    }

    /// <summary>One tile as PNG, or null if no pixel of the image falls in it.</summary>
    public byte[]? Render(double minX, double minY, double maxX, double maxY)
    {
        const int Nodes = (TileSize / GridStep) + 1;
        var gridX = new double[Nodes, Nodes];
        var gridY = new double[Nodes, Nodes];
        for (var j = 0; j < Nodes; j++)
        {
            for (var i = 0; i < Nodes; i++)
            {
                var wx = minX + ((maxX - minX) * i * GridStep / TileSize);
                var wy = maxY - ((maxY - minY) * j * GridStep / TileSize);
                try
                {
                    var (px, py) = _map.ToPixel(WebMercator.ToGeo(wx, wy));
                    (gridX[i, j], gridY[i, j]) = (px * _image.Scale, py * _image.Scale);
                }
                catch (UnsupportedCrsException)
                {
                    (gridX[i, j], gridY[i, j]) = (double.NaN, double.NaN);
                }
            }
        }

        var rgba = new byte[TileSize * TileSize * 4];
        var source = _image.Bgra;
        var any = false;
        for (var y = 0; y < TileSize; y++)
        {
            var gj = y / GridStep;
            var fy = ((y + 0.5) - (gj * GridStep)) / GridStep;
            for (var x = 0; x < TileSize; x++)
            {
                var gi = x / GridStep;
                var fx = ((x + 0.5) - (gi * GridStep)) / GridStep;
                var sx = Lerp(gridX, gi, gj, fx, fy);
                var sy = Lerp(gridY, gi, gj, fx, fy);
                if (!(sx >= 0 && sy >= 0 && sx < _image.Width && sy < _image.Height))
                {
                    continue; // NaN or outside: transparent
                }

                var s = (((int)sy * _image.Width) + (int)sx) * 4;
                var d = ((y * TileSize) + x) * 4;
                rgba[d] = source[s + 2];
                rgba[d + 1] = source[s + 1];
                rgba[d + 2] = source[s];
                rgba[d + 3] = source[s + 3];
                any = true;
            }
        }

        return any ? PngEncoder.Encode(TileSize, TileSize, rgba) : null;
    }

    private static double Lerp(double[,] grid, int i, int j, double fx, double fy)
    {
        var top = grid[i, j] + ((grid[i + 1, j] - grid[i, j]) * fx);
        var bottom = grid[i, j + 1] + ((grid[i + 1, j + 1] - grid[i, j + 1]) * fx);
        return top + ((bottom - top) * fy);
    }
}
