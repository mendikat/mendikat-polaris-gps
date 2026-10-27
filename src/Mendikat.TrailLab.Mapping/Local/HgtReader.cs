using System.Buffers.Binary;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Mendikat.TrailLab.Mapping.Local;

/// <summary>
/// Reads SRTM HGT tiles (LDEM-02): 1201 × 1201 (3″) or 3601 × 3601 (1″) signed 16-bit big-endian samples, rows from
/// north to south, nodes on the tile edges (the first row is the northern edge). The south-west corner comes from the
/// file name (N42W001 = 42° N, 1° W). −32768 is a void: no data. Positions are WGS84; heights refer to the EGM96 geoid
/// according to the SRTM specification (not converted).
/// </summary>
public static partial class HgtReader
{
    public const short Void = short.MinValue;

    /// <exception cref="LocalMapException">Name without coordinates, wrong size, or unreadable.</exception>
    public static ElevationGrid Read(string path, CancellationToken cancellationToken = default)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var match = TileName().Match(name);
        if (!match.Success)
        {
            throw new LocalMapException(LocalMapError.InvalidDem, $"el nombre «{name}» no indica la esquina (p. ej. N42W001)");
        }

        var south = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * (match.Groups[1].Value.Equals("S", StringComparison.OrdinalIgnoreCase) ? -1 : 1);
        var west = int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture) * (match.Groups[3].Value.Equals("W", StringComparison.OrdinalIgnoreCase) ? -1 : 1);
        if (south is < -90 or >= 90 || west is < -180 or >= 180)
        {
            throw new LocalMapException(LocalMapError.InvalidDem, $"esquina «{name}» fuera de rango");
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new LocalMapException(LocalMapError.ReadFailed, Path.GetFileName(path), innerException: ex);
        }

        var size = bytes.Length switch
        {
            1201 * 1201 * 2 => 1201,
            3601 * 3601 * 2 => 3601,
            0 => throw new LocalMapException(LocalMapError.EmptyFile),
            _ => throw new LocalMapException(LocalMapError.InvalidDem, $"{bytes.Length} bytes (un HGT tiene 1201² o 3601² muestras de 2 bytes)"),
        };

        var values = new float[size * size];
        for (var i = 0; i < values.Length; i++)
        {
            if (i % (size * 64) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var sample = BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(i * 2, 2));
            values[i] = sample == Void ? float.NaN : sample;
        }

        var cell = 1.0 / (size - 1);
        return new ElevationGrid(
            Path.GetFileName(path), Crs.Geographic(MapDatum.Wgs84), size, size, west, south + 1, cell, cell, values,
            "Alturas sobre el geoide EGM96 (especificación SRTM). Sin corrección.");
    }

    [GeneratedRegex(@"^([NS])(\d{2})([EW])(\d{3})", RegexOptions.IgnoreCase)]
    private static partial Regex TileName();
}
