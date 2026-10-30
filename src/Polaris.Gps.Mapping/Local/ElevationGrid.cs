using Polaris.Gps.Domain.Elevation;
using Polaris.Gps.Domain.Geo;

namespace Polaris.Gps.Mapping.Local;

/// <summary>
/// A local DEM held in memory (LDEM-01): values at the nodes of a regular grid in a coordinate system, row 0 at the
/// north. Elevations are interpolated bilinearly; a position outside the grid, or next to a node without data, has
/// no data (never 0 m; LDEM-06). It is an <see cref="IElevationSource"/>, so the status bar and "Adjust elevations"
/// can use it.
/// </summary>
public sealed class ElevationGrid : ILocalDem
{
    private readonly float[] _values;

    /// <param name="x0">X (or longitude) of the nodes of column 0.</param>
    /// <param name="y0">Y (or latitude) of the nodes of row 0, the northernmost.</param>
    /// <param name="values">Rows × columns values from north to south; NaN = no data.</param>
    public ElevationGrid(string name, Crs crs, int columns, int rows, double x0, double y0, double cellX, double cellY, float[] values, string verticalDatum)
    {
        if (columns < 2 || rows < 2 || values.Length != columns * rows || cellX <= 0 || cellY <= 0)
        {
            throw new LocalMapException(LocalMapError.InvalidDem, $"rejilla de {columns} × {rows}");
        }

        Name = name;
        Crs = crs;
        Columns = columns;
        Rows = rows;
        X0 = x0;
        Y0 = y0;
        CellX = cellX;
        CellY = cellY;
        _values = values;
        VerticalDatum = verticalDatum;
        Bounds = ComputeBounds();
    }

    public string Name { get; }

    public string VerticalDatum { get; }

    public string Attribution => $"MDT local «{Name}»";

    public Crs Crs { get; }

    public string CrsDescription => Crs.Description;

    public int Columns { get; }

    public int Rows { get; }

    public double X0 { get; }

    public double Y0 { get; }

    public double CellX { get; }

    public double CellY { get; }

    /// <summary>WGS84 extent of the grid.</summary>
    public GeoBounds Bounds { get; }

    /// <summary>Value of a node, or null if it has no data.</summary>
    public double? this[int row, int column] => float.IsNaN(_values[(row * Columns) + column]) ? null : _values[(row * Columns) + column];

    public Task<IReadOnlyList<double?>> GetElevationsAsync(
        IReadOnlyList<GeoPosition> positions, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var result = new double?[positions.Count];
        for (var i = 0; i < positions.Count; i++)
        {
            if (i % 4096 == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            result[i] = ElevationAt(positions[i]);
        }

        progress?.Report(1);
        return Task.FromResult<IReadOnlyList<double?>>(result);
    }

    /// <summary>Bilinear elevation at a WGS84 position, or null.</summary>
    public double? ElevationAt(GeoPosition position)
    {
        if (position.Latitude < Bounds.MinLatitude - 0.01 || position.Latitude > Bounds.MaxLatitude + 0.01
            || position.Longitude < Bounds.MinLongitude - 0.01 || position.Longitude > Bounds.MaxLongitude + 0.01)
        {
            return null;
        }

        double x, y;
        try
        {
            (x, y) = Crs.FromWgs84(position);
        }
        catch (UnsupportedCrsException)
        {
            return null;
        }

        // A position exactly on the edge may come back a hair outside after the coordinate conversions.
        const double Edge = 1e-6;
        var col = (x - X0) / CellX;
        var row = (Y0 - y) / CellY;
        if (!(col >= -Edge && row >= -Edge && col <= Columns - 1 + Edge && row <= Rows - 1 + Edge))
        {
            return null;
        }

        col = Math.Clamp(col, 0, Columns - 1);
        row = Math.Clamp(row, 0, Rows - 1);

        var c = Math.Min((int)col, Columns - 2);
        var r = Math.Min((int)row, Rows - 2);
        var fx = col - c;
        var fy = row - r;
        // A node without data spoils the result only if it weighs in it (a position on a node or an edge
        // ignores the neighbours it does not touch).
        Span<(float Value, double Weight)> corners =
        [
            (_values[(r * Columns) + c], (1 - fx) * (1 - fy)),
            (_values[(r * Columns) + c + 1], fx * (1 - fy)),
            (_values[((r + 1) * Columns) + c], (1 - fx) * fy),
            (_values[((r + 1) * Columns) + c + 1], fx * fy),
        ];
        double sum = 0, total = 0;
        foreach (var (value, weight) in corners)
        {
            if (weight < 1e-4)
            {
                continue;
            }

            if (float.IsNaN(value))
            {
                return null;
            }

            sum += value * weight;
            total += weight;
        }

        return total > 0 ? sum / total : null;
    }

    private GeoBounds ComputeBounds()
    {
        var edge = new List<GeoPosition>();
        const int Steps = 8;
        var width = (Columns - 1) * CellX;
        var height = (Rows - 1) * CellY;
        for (var i = 0; i <= Steps; i++)
        {
            var t = (double)i / Steps;
            foreach (var (x, y) in new[] { (X0 + (t * width), Y0), (X0 + (t * width), Y0 - height), (X0, Y0 - (t * height)), (X0 + width, Y0 - (t * height)) })
            {
                edge.Add(Crs.ToWgs84(x, y));
            }
        }

        if (edge.Any(p => !GeoPosition.IsValidLatitude(p.Latitude) || !GeoPosition.IsValidLongitude(p.Longitude)))
        {
            throw new LocalMapException(LocalMapError.InvalidDem, "la rejilla queda fuera de la Tierra");
        }

        return GeoBounds.FromPositions(edge)!.Value;
    }
}
