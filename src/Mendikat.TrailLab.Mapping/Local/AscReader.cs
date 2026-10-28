using System.Globalization;
using System.Text;

namespace Mendikat.TrailLab.Mapping.Local;

/// <summary>
/// Reads ESRI ASCII grids (.asc; LDEM-04): header ncols, nrows, xllcorner/xllcenter, yllcorner/yllcenter, cellsize
/// and optional NODATA_value, then nrows × ncols values from north to south. "corner" headers give the outer corner
/// of the lower-left cell, "center" headers its centre; both give the same node positions. The coordinate system comes
/// from the .prj file next to it; without it the grid is rejected (D10), because guessing could place it far away.
/// </summary>
public static class AscReader
{
    /// <summary>Largest grid accepted (values held as 4-byte floats: 400 MB).</summary>
    public const long MaxValues = 100_000_000;

    /// <exception cref="LocalMapException">No coordinate system, invalid header, wrong number of values, or too large.</exception>
    /// <exception cref="OperationCanceledException">Cancelled.</exception>
    public static ElevationGrid Read(string path, CancellationToken cancellationToken = default)
    {
        var (crs, note) = CoordinateSystem(path);
        try
        {
            using var reader = new StreamReader(path, Encoding.Latin1, detectEncodingFromByteOrderMarks: true, bufferSize: 1 << 16);
            return Read(reader, Path.GetFileName(path), crs, cancellationToken, note);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new LocalMapException(LocalMapError.ReadFailed, Path.GetFileName(path), innerException: ex);
        }
    }

    /// <summary>
    /// The grid's coordinate system. An IGN file name ("MDT05-0112-H30-LIDAR", "PNOA_MDT05_ETRS89_HU30_0112_LID")
    /// states its UTM zone and datum and wins over a .prj that says otherwise: IGN grids are often shared with .prj
    /// files made later by conversion tools, and some give the wrong zone (D10). Any other grid needs its .prj.
    /// </summary>
    /// <returns>The system and, when it does not come from the .prj, a note for the user.</returns>
    internal static (Crs Crs, string? Note) CoordinateSystem(string path)
    {
        var prj = FindPrj(path);
        Crs? fromPrj = null;
        string? prjProblem = null;
        if (prj is not null)
        {
            try
            {
                fromPrj = PrjReader.Parse(File.ReadAllText(prj));
            }
            catch (UnsupportedCrsException ex)
            {
                prjProblem = ex.Detail;
            }
        }

        if (IgnName.Crs(Path.GetFileNameWithoutExtension(path)) is { } ign)
        {
            // WGS84, ETRS89 and REGCAN95 are the same at map scale: only another zone or ED50 is a real disagreement.
            var disagrees = fromPrj is not null && (fromPrj.UtmZone != ign.UtmZone || fromPrj.Datum == MapDatum.Ed50 || fromPrj.IsGeographic);
            var note = disagrees
                ? $"Sistema según el nombre del IGN ({ign}); el .prj dice {fromPrj} y no se usa."
                : fromPrj is null ? $"Sistema según el nombre del IGN ({ign})." : null;
            return (ign, note);
        }

        if (fromPrj is not null)
        {
            return (fromPrj, null);
        }

        if (prjProblem is not null)
        {
            throw new LocalMapException(LocalMapError.UnsupportedCrs, prjProblem);
        }

        throw new LocalMapException(LocalMapError.MissingProjection, Path.GetFileNameWithoutExtension(path).Trim() + ".prj");
    }

    internal static ElevationGrid Read(TextReader reader, string name, Crs crs, CancellationToken cancellationToken, string? note = null)

    {
        var tokens = new Tokenizer(reader);
        var header = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        string? token;
        while ((token = tokens.Next()) is not null && !IsNumber(token))
        {
            var value = tokens.Next();
            if (value is null || !TryNumber(value, out var number))
            {
                throw new LocalMapException(LocalMapError.InvalidDem, $"cabecera «{token}» sin valor", tokens.Line);
            }

            header[token] = number;
        }

        if (token is null && header.Count == 0)
        {
            throw new LocalMapException(LocalMapError.EmptyFile);
        }

        var columns = (int)Required(header, "ncols", tokens.Line);
        var rows = (int)Required(header, "nrows", tokens.Line);
        var cell = Required(header, "cellsize", tokens.Line);
        if (columns < 2 || rows < 2 || cell <= 0)
        {
            throw new LocalMapException(LocalMapError.InvalidDem, $"ncols={columns}, nrows={rows}, cellsize={cell}");
        }

        if ((long)columns * rows > MaxValues)
        {
            throw new LocalMapException(LocalMapError.TooLarge, $"{columns} × {rows}");
        }

        var x0 = header.TryGetValue("xllcenter", out var xc) ? xc
            : header.TryGetValue("xllcorner", out var xk) ? xk + (cell / 2)
            : throw new LocalMapException(LocalMapError.InvalidDem, "falta xllcorner o xllcenter");
        var ySouth = header.TryGetValue("yllcenter", out var yc) ? yc
            : header.TryGetValue("yllcorner", out var yk) ? yk + (cell / 2)
            : throw new LocalMapException(LocalMapError.InvalidDem, "falta yllcorner o yllcenter");
        double? noData = header.TryGetValue("nodata_value", out var nd) ? nd : null;

        var values = new float[columns * rows];
        for (var i = 0; i < values.Length; i++)
        {
            if (i % 65536 == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (i > 0 || token is null)
            {
                token = tokens.Next();
            }

            if (token is null)
            {
                throw new LocalMapException(LocalMapError.InvalidDem, $"faltan valores: {i} de {values.Length}", tokens.Line);
            }

            if (!TryNumber(token, out var v))
            {
                throw new LocalMapException(LocalMapError.InvalidDem, $"valor «{(token.Length > 20 ? token[..20] : token)}»", tokens.Line);
            }

            values[i] = noData is { } n && v == n ? float.NaN : (float)v;
        }

        if (tokens.Next() is not null)
        {
            throw new LocalMapException(LocalMapError.InvalidDem, $"sobran valores después de {values.Length}", tokens.Line);
        }

        var unit = crs.IsGeographic ? "grados" : "metros";
        var vertical = IgnName.Crs(Path.GetFileNameWithoutExtension(name)) is not null
            ? "Altitudes ortométricas del IGN (REDNAP), según la especificación del producto; el archivo no lo indica"
            : "Referencia vertical no indicada en el archivo (desconocida)";
        return new ElevationGrid(
            name, crs, columns, rows, x0, ySouth + ((rows - 1) * cell), cell, cell, values,
            $"{vertical}; celdas de {cell.ToString(CultureInfo.InvariantCulture)} {unit}, {crs}.{(note is null ? string.Empty : " " + note)}");
    }

    /// <summary>"name.prj" or "name.asc.prj", whatever the case and the spaces around the name (<see cref="LocalFiles.FindSidecar"/>).</summary>
    internal static string? FindPrj(string path) => LocalFiles.FindSidecar(path, ".prj");

    private static double Required(Dictionary<string, double> header, string key, int line) =>
        header.TryGetValue(key, out var value) ? value : throw new LocalMapException(LocalMapError.InvalidDem, $"falta «{key}»", line);

    private static bool IsNumber(string token) => token.Length > 0 && (char.IsDigit(token[0]) || token[0] is '-' or '+' or '.');

    private static bool TryNumber(string token, out double value) =>
        double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    /// <summary>White-space separated tokens with line numbers, without loading the whole file as one string.</summary>
    private sealed class Tokenizer(TextReader reader)
    {
        private readonly StringBuilder _token = new();

        public int Line { get; private set; } = 1;

        public string? Next()
        {
            _token.Clear();
            int c;
            while ((c = reader.Read()) >= 0)
            {
                if (c == 0x1A)
                {
                    break; // Ctrl+Z: the old MS-DOS end-of-file mark, found at the end of IGN grids
                }

                if (c == '\n')
                {
                    Line++;
                }

                if (char.IsWhiteSpace((char)c))
                {
                    if (_token.Length > 0)
                    {
                        return _token.ToString();
                    }

                    continue;
                }

                _token.Append((char)c);
            }

            return _token.Length > 0 ? _token.ToString() : null;
        }
    }
}
