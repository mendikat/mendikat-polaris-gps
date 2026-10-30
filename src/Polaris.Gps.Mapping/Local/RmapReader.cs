using System.Text;

namespace Polaris.Gps.Mapping.Local;

/// <summary>One zoom level of an RMAP: its size in pixels and where each tile starts (row by row from the top left).</summary>
public sealed record RmapLevel(int Width, int Height, int TilesX, int TilesY, IReadOnlyList<long> TileOffsets);

/// <summary>What the header of an RMAP says: full size, tile size, zoom levels and the calibration text.</summary>
public sealed record RmapHeader(int Width, int Height, int TileWidth, int TileHeight, IReadOnlyList<RmapLevel> Levels, IReadOnlyList<string> CalibrationLines);

/// <summary>
/// Reads TwoNav/CompeGPS raster maps (.rmap). There is no published specification; the layout follows the reader of
/// QMapShack (CMapRMAP) and the "TwoNav RMAP" page of the OpenStreetMap wiki. Little-endian:
/// <list type="bullet">
/// <item>"CompeGPSRasterImage" (19 bytes), two type numbers (10 and 7 for JPEG tiles) and one unused uint32;</item>
/// <item>width and height (int32, the height negative), two unused uint32, tile width and height (uint32);</item>
/// <item>offset of the calibration text (uint64), one unused uint32, the number of zoom levels (int32) and the offset of
/// each level (uint64);</item>
/// <item>each level: width, height, tiles across and down (int32) and the offset of each tile (uint64); a tile is a
/// tag and a length (uint32) followed by a JPEG of up to the tile size (smaller at the right and bottom edges);</item>
/// <item>the calibration: an unused uint32, its length (uint32) and the text of an .imp without "Bitmap=", whose
/// points refer to the pixels of the full size.</item>
/// </list>
/// The pixels are decoded from the largest level that fits in memory (a level is a reduced copy of the whole map).
/// </summary>
public static class RmapReader
{
    private const string Signature = "CompeGPSRasterImage";
    private const int MaxTileSize = 4096;
    private const int MaxLevels = 64;
    private const int MaxCalibrationBytes = 1 << 20;

    /// <exception cref="LocalMapException">Not a valid RMAP, of an unsupported type, or its calibration is invalid.</exception>
    /// <exception cref="OperationCanceledException">Cancelled.</exception>
    public static LocalRasterMap Load(string path, IRasterImageDecoder decoder, long maxPixels, CancellationToken cancellationToken = default)
    {
        var name = Path.GetFileName(path);
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream, Encoding.Latin1);
            var header = ReadHeader(reader, name);
            var calibration = CompeImpReader.ParseEmbedded(header.CalibrationLines, path);
            var map = GeoreferencedMap.Create(calibration, header.Width, header.Height);
            var level = ChooseLevel(header.Levels, maxPixels);
            if ((long)level.Width * level.Height > maxPixels)
            {
                throw new LocalMapException(LocalMapError.TooLarge, $"{level.Width} × {level.Height}");
            }

            var image = Assemble(reader, header, level, decoder, name, cancellationToken);
            return new LocalRasterMap(path, map, image);
        }
        catch (EndOfStreamException ex)
        {
            throw new LocalMapException(LocalMapError.InvalidCalibration, "archivo RMAP incompleto o dañado", innerException: ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new LocalMapException(LocalMapError.ReadFailed, name, innerException: ex);
        }
    }

    /// <exception cref="LocalMapException">Not a valid RMAP or of an unsupported type.</exception>
    /// <exception cref="EndOfStreamException">The file ends too soon.</exception>
    public static RmapHeader ReadHeader(BinaryReader reader, string name)
    {
        var length = reader.BaseStream.Length;
        if (Encoding.Latin1.GetString(reader.ReadBytes(Signature.Length)) != Signature)
        {
            throw new LocalMapException(LocalMapError.InvalidCalibration, "no es un mapa RMAP de TwoNav");
        }

        var type = reader.ReadUInt32();
        var subtype = reader.ReadUInt32();
        if (type != 10 || subtype != 7)
        {
            throw new LocalMapException(LocalMapError.UnsupportedRmap, $"tipo {type}.{subtype}");
        }

        reader.ReadUInt32();
        var width = reader.ReadInt32();
        var height = Math.Abs(reader.ReadInt32());
        reader.ReadUInt32();
        reader.ReadUInt32();
        var tileWidth = reader.ReadUInt32();
        var tileHeight = reader.ReadUInt32();
        var calibrationOffset = reader.ReadUInt64();
        reader.ReadUInt32();
        var levelCount = reader.ReadInt32();
        if (width <= 0 || height <= 0 || tileWidth is 0 or > MaxTileSize || tileHeight is 0 or > MaxTileSize
            || levelCount is <= 0 or > MaxLevels || calibrationOffset >= (ulong)length)
        {
            throw Damaged();
        }

        var levelOffsets = new ulong[levelCount];
        for (var i = 0; i < levelCount; i++)
        {
            levelOffsets[i] = reader.ReadUInt64();
        }

        var levels = new List<RmapLevel>();
        foreach (var offset in levelOffsets)
        {
            if (offset >= (ulong)length)
            {
                throw Damaged();
            }

            reader.BaseStream.Position = (long)offset;
            var levelWidth = reader.ReadInt32();
            var levelHeight = Math.Abs(reader.ReadInt32());
            var tilesX = reader.ReadInt32();
            var tilesY = reader.ReadInt32();
            if (levelWidth <= 0 || levelHeight <= 0 || tilesX <= 0 || tilesY <= 0 || (long)tilesX * tilesY * 8 > length)
            {
                throw Damaged();
            }

            var tiles = new long[tilesX * tilesY];
            for (var i = 0; i < tiles.Length; i++)
            {
                var tile = reader.ReadUInt64();
                tiles[i] = tile < (ulong)length ? (long)tile : 0;
            }

            levels.Add(new RmapLevel(levelWidth, levelHeight, tilesX, tilesY, tiles));
        }

        reader.BaseStream.Position = (long)calibrationOffset;
        reader.ReadUInt32();
        var textLength = reader.ReadUInt32();
        if (textLength > MaxCalibrationBytes)
        {
            throw Damaged();
        }

        var text = Encoding.Latin1.GetString(reader.ReadBytes((int)textLength)).TrimEnd('\0');
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        return new RmapHeader(width, height, (int)tileWidth, (int)tileHeight, levels, lines);
    }

    /// <summary>The largest level with at most <paramref name="maxPixels"/> pixels, or else the smallest one.</summary>
    public static RmapLevel ChooseLevel(IReadOnlyList<RmapLevel> levels, long maxPixels)
    {
        static long Pixels(RmapLevel level) => (long)level.Width * level.Height;
        return levels.Where(l => Pixels(l) <= maxPixels).MaxBy(Pixels) ?? levels.MinBy(Pixels)!;
    }

    private static RasterImage Assemble(BinaryReader reader, RmapHeader header, RmapLevel level, IRasterImageDecoder decoder, string name, CancellationToken cancellationToken)
    {
        var length = reader.BaseStream.Length;
        var pixels = new byte[(long)level.Width * level.Height * 4];
        var decoded = 0;
        for (var ty = 0; ty < level.TilesY; ty++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var tx = 0; tx < level.TilesX; tx++)
            {
                var offset = level.TileOffsets[(ty * level.TilesX) + tx];
                if (offset == 0)
                {
                    continue;
                }

                reader.BaseStream.Position = offset;
                reader.ReadUInt32();
                var size = reader.ReadUInt32();
                if (size == 0 || size > length - reader.BaseStream.Position)
                {
                    continue;
                }

                RasterImage tile;
                try
                {
                    tile = decoder.Decode(reader.ReadBytes((int)size), name);
                }
                catch (LocalMapException)
                {
                    continue; // A damaged tile leaves a hole, like in TwoNav and QMapShack.
                }

                Copy(tile, pixels, level.Width, level.Height, tx * header.TileWidth, ty * header.TileHeight);
                decoded++;
            }
        }

        if (decoded == 0)
        {
            throw new LocalMapException(LocalMapError.InvalidImage, name);
        }

        return new RasterImage(level.Width, level.Height, (double)level.Width / header.Width, pixels);
    }

    /// <summary>Copies a tile into the image at (x, y), cut at the right and bottom edges.</summary>
    private static void Copy(RasterImage tile, byte[] pixels, int width, int height, int x, int y)
    {
        var columns = Math.Min(tile.Width, width - x);
        var rows = Math.Min(tile.Height, height - y);
        if (columns <= 0 || rows <= 0)
        {
            return;
        }

        for (var row = 0; row < rows; row++)
        {
            Buffer.BlockCopy(tile.Bgra, row * tile.Width * 4, pixels, (((y + row) * width) + x) * 4, columns * 4);
        }
    }

    private static LocalMapException Damaged() =>
        new(LocalMapError.InvalidCalibration, "archivo RMAP incompleto o dañado");
}
