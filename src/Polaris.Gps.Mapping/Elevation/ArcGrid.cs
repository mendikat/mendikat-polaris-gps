using System.Globalization;

namespace Polaris.Gps.Mapping.Elevation;

/// <summary>
/// An Esri ASCII grid (ArcGrid) in geographic degrees, as returned by the IGN WCS. Rows go from north to south.
/// Values equal to the NODATA value, if declared, are no data.
/// </summary>
public sealed class ArcGrid
{
    private readonly double[] _values;

    private ArcGrid(int columns, int rows, double west, double south, double cellSize, double? noData, double[] values)
    {
        Columns = columns;
        Rows = rows;
        West = west;
        South = south;
        CellSize = cellSize;
        NoData = noData;
        _values = values;
    }

    public int Columns { get; }

    public int Rows { get; }

    /// <summary>Longitude of the left edge (xllcorner).</summary>
    public double West { get; }

    /// <summary>Latitude of the bottom edge (yllcorner).</summary>
    public double South { get; }

    public double CellSize { get; }

    public double? NoData { get; }

    /// <summary>Value of a cell; row 0 is the northernmost.</summary>
    public double this[int row, int column] => _values[row * Columns + column];

    /// <summary>
    /// Parses the grid. The text may be wrapped in a MIME multipart response: anything before the "ncols" header
    /// and after the values (the closing boundary) is ignored.
    /// </summary>
    /// <exception cref="FormatException">The text is not a valid grid.</exception>
    public static ArcGrid Parse(string text)
    {
        var start = text.IndexOf("ncols", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            throw new FormatException("No ArcGrid header found.");
        }

        var tokens = text[start..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var header = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        while (index + 1 < tokens.Length && !IsNumber(tokens[index]))
        {
            header[tokens[index]] = Number(tokens[index + 1]);
            index += 2;
        }

        var columns = (int)Required(header, "ncols");
        var rows = (int)Required(header, "nrows");
        var west = header.GetValueOrDefault("xllcorner", double.NaN);
        var south = header.GetValueOrDefault("yllcorner", double.NaN);
        var cellSize = Required(header, "cellsize");
        if (double.IsNaN(west) || double.IsNaN(south) || columns <= 0 || rows <= 0 || cellSize <= 0)
        {
            throw new FormatException("Invalid ArcGrid header.");
        }

        var values = new double[columns * rows];
        for (var i = 0; i < values.Length; i++)
        {
            if (index >= tokens.Length || !IsNumber(tokens[index]))
            {
                throw new FormatException($"Expected {values.Length} values, found {i}.");
            }

            values[i] = Number(tokens[index++]);
        }

        double? noData = header.TryGetValue("NODATA_value", out var nd) ? nd : null;
        return new ArcGrid(columns, rows, west, south, cellSize, noData, values);
    }

    /// <summary>
    /// Bilinear interpolation between the four nearest cell centres. Null if the position is outside the grid
    /// or any of the four cells has no data (<paramref name="isNoData"/> or the declared NODATA value).
    /// </summary>
    public double? Interpolate(double latitude, double longitude, Func<double, bool> isNoData)
    {
        // Continuous cell coordinates of the position, relative to cell centres.
        var column = (longitude - West) / CellSize - 0.5;
        var row = (South + Rows * CellSize - latitude) / CellSize - 0.5;
        var c0 = (int)Math.Floor(column);
        var r0 = (int)Math.Floor(row);
        c0 = Math.Clamp(c0, 0, Columns - 2);
        r0 = Math.Clamp(r0, 0, Rows - 2);
        if (column < -0.5 || row < -0.5 || column > Columns - 0.5 || row > Rows - 0.5 || Columns < 2 || Rows < 2)
        {
            return null;
        }

        var fx = Math.Clamp(column - c0, 0, 1);
        var fy = Math.Clamp(row - r0, 0, 1);
        double[] values = [this[r0, c0], this[r0, c0 + 1], this[r0 + 1, c0], this[r0 + 1, c0 + 1]];
        double[] weights = [(1 - fx) * (1 - fy), fx * (1 - fy), (1 - fx) * fy, fx * fy];
        var result = 0.0;
        for (var i = 0; i < 4; i++)
        {
            if (weights[i] == 0)
            {
                continue; // a cell that does not contribute cannot invalidate the result
            }

            if (isNoData(values[i]) || (NoData is { } nd && values[i] == nd))
            {
                return null;
            }

            result += values[i] * weights[i];
        }

        return result;
    }

    private static bool IsNumber(string token) =>
        double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    private static double Number(string token) =>
        double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new FormatException($"Invalid number '{token}'.");

    private static double Required(Dictionary<string, double> header, string key) =>
        header.TryGetValue(key, out var value) ? value : throw new FormatException($"Missing '{key}'.");
}
