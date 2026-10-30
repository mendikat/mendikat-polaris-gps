using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Polaris.Gps.App.Map;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Mapping.Local;

namespace Polaris.Gps.Tests.Mapping;

/// <summary>
/// TwoNav RMAP maps. The files are built here with the layout that QMapShack reads: JPEG tiles of one colour each,
/// so the decoded pixels show which tile and which zoom level were used.
/// </summary>
public sealed class RmapReaderTests : IDisposable
{
    private static readonly Color Red = Color.FromRgb(255, 0, 0);
    private static readonly Color Blue = Color.FromRgb(0, 0, 255);
    private static readonly Color Green = Color.FromRgb(0, 255, 0);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "polarisgps-rmap-" + Guid.NewGuid().ToString("N")[..8]);

    public RmapReaderTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private const string Geographic = """
        CompeGPS MAP File
        <Header>
        Version=2
        Projection=1,Latitude/Longitude,
        Datum=WGS 84
        </Header>
        <Calibration>
        P0=0,0,A,-2.0,43.0
        P1=300,200,A,-1.0,42.5
        </Calibration>
        """;

    /// <summary>One zoom level: its size and the colour of each tile (row by row; null = no tile).</summary>
    private sealed record Level(int Width, int Height, params Color?[] Tiles);

    /// <summary>Writes an RMAP of <paramref name="width"/> × <paramref name="height"/> with 256-pixel tiles.</summary>
    private string Rmap(string name, int width, int height, string calibration, Level[] levels, uint type = 10, uint subtype = 7)
    {
        const int Tile = 256;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Encoding.ASCII.GetBytes("CompeGPSRasterImage"));
        writer.Write(type);
        writer.Write(subtype);
        writer.Write(0u);
        writer.Write(width);
        writer.Write(-height);
        writer.Write(24u);
        writer.Write(1u);
        writer.Write((uint)Tile);
        writer.Write((uint)Tile);
        var calibrationOffsetAt = stream.Position;
        writer.Write(0ul);
        writer.Write(0u);
        writer.Write(levels.Length);
        var levelOffsetsAt = stream.Position;
        foreach (var _ in levels)
        {
            writer.Write(0ul);
        }

        for (var l = 0; l < levels.Length; l++)
        {
            var level = levels[l];
            var tilesX = (level.Width + Tile - 1) / Tile;
            var tilesY = (level.Height + Tile - 1) / Tile;
            Patch(writer, levelOffsetsAt + (l * 8), (ulong)stream.Position);
            writer.Write(level.Width);
            writer.Write(level.Height);
            writer.Write(tilesX);
            writer.Write(tilesY);
            var tileOffsetsAt = stream.Position;
            for (var i = 0; i < tilesX * tilesY; i++)
            {
                writer.Write(0ul);
            }

            for (var i = 0; i < tilesX * tilesY; i++)
            {
                if (level.Tiles[i] is not { } colour)
                {
                    continue;
                }

                var tileWidth = Math.Min(Tile, level.Width - ((i % tilesX) * Tile));
                var tileHeight = Math.Min(Tile, level.Height - ((i / tilesX) * Tile));
                var jpeg = Jpeg(tileWidth, tileHeight, colour);
                Patch(writer, tileOffsetsAt + (i * 8), (ulong)stream.Position);
                writer.Write(7u);
                writer.Write((uint)jpeg.Length);
                writer.Write(jpeg);
            }
        }

        Patch(writer, calibrationOffsetAt, (ulong)stream.Position);
        var text = Encoding.Latin1.GetBytes(calibration.ReplaceLineEndings("\r\n"));
        writer.Write(1u);
        writer.Write((uint)text.Length);
        writer.Write(text);
        writer.Flush();

        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, stream.ToArray());
        return path;
    }

    private static void Patch(BinaryWriter writer, long at, ulong value)
    {
        var end = writer.BaseStream.Position;
        writer.BaseStream.Position = at;
        writer.Write(value);
        writer.BaseStream.Position = end;
    }

    private static byte[] Jpeg(int width, int height, Color colour)
    {
        var pixels = new byte[width * height * 3];
        for (var i = 0; i < pixels.Length; i += 3)
        {
            (pixels[i], pixels[i + 1], pixels[i + 2]) = (colour.R, colour.G, colour.B);
        }

        var encoder = new JpegBitmapEncoder { QualityLevel = 100 };
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(width, height, 96, 96, PixelFormats.Rgb24, null, pixels, width * 3)));
        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }

    private static LocalRasterMap Load(string path, long maxPixels = LocalMapLoader.MaxDecodedPixels) =>
        RmapReader.Load(path, new WpfRasterImageDecoder(), maxPixels, TestContext.Current.CancellationToken);

    /// <summary>Asserts the colour of a decoded pixel, allowing for JPEG rounding; transparent when <paramref name="expected"/> is null.</summary>
    private static void AssertPixel(Color? expected, RasterImage image, int x, int y)
    {
        var i = ((y * image.Width) + x) * 4;
        var (b, g, r, a) = (image.Bgra[i], image.Bgra[i + 1], image.Bgra[i + 2], image.Bgra[i + 3]);
        if (expected is not { } colour)
        {
            Assert.Equal(0, a);
            return;
        }

        Assert.Equal(255, a);
        Assert.InRange(Math.Abs(r - colour.R) + Math.Abs(g - colour.G) + Math.Abs(b - colour.B), 0, 24);
    }

    [Fact]
    public void An_rmap_is_opened_at_full_size_with_its_tiles_in_place_and_its_embedded_calibration()
    {
        var path = Rmap("mapa.rmap", 300, 200, Geographic, [new Level(300, 200, Red, Blue), new Level(150, 100, Green)]);

        var raster = Assert.IsType<LocalRasterMap>(LocalMapLoader.Load(path, new WpfRasterImageDecoder(), TestContext.Current.CancellationToken));

        Assert.Equal((300, 200, 1.0), (raster.Image.Width, raster.Image.Height, raster.Image.Scale));
        AssertPixel(Red, raster.Image, 10, 10);
        AssertPixel(Red, raster.Image, 255, 199);
        AssertPixel(Blue, raster.Image, 256, 0);
        AssertPixel(Blue, raster.Image, 299, 199);
        Assert.Equal("CompeGPS", raster.Map.Calibration.Format);
        Assert.Equal(path, raster.Map.Calibration.ImagePath);
        Assert.Equal((300, 200), (raster.Map.ImageWidth, raster.Map.ImageHeight));
        var centre = raster.Map.ToWgs84(150, 100);
        Assert.Equal(42.75, centre.Latitude, 1e-9);
        Assert.Equal(-1.5, centre.Longitude, 1e-9);
    }

    [Fact]
    public void A_big_rmap_uses_its_largest_reduced_level_that_fits_in_memory()
    {
        var path = Rmap("mapa.rmap", 300, 200, Geographic, [new Level(300, 200, Red, Blue), new Level(150, 100, Green), new Level(75, 50, Blue)]);

        var raster = Load(path, maxPixels: 20_000);

        Assert.Equal((150, 100, 0.5), (raster.Image.Width, raster.Image.Height, raster.Image.Scale));
        AssertPixel(Green, raster.Image, 75, 50);
        Assert.Equal((300, 200), (raster.Map.ImageWidth, raster.Map.ImageHeight));
    }

    [Fact]
    public void Utm_points_give_the_same_placement_as_the_same_imp()
    {
        var path = Rmap("utm.rmap", 200, 100, """
            CompeGPS MAP File
            <Header>
            Version=2
            Projection=0,UTM,30,N,
            Datum=European 1950
            </Header>
            <Calibration>
            P0=0.00000000,0.00000000,30T,430000.00000000,4780000.00000000
            P1=200.00000000,0.00000000,30T,432000.00000000,4780000.00000000
            P2=200.00000000,100.00000000,30T,432000.00000000,4779000.00000000
            P3=0.00000000,100.00000000,30T,430000.00000000,4779000.00000000
            </Calibration>
            """, [new Level(200, 100, Red)]);

        var corner = Load(path).Map.ToWgs84(0, 0);

        // The values of LocalMapReaderTests.Compegps_imp_utm_ed50_gives_the_same_placement_as_ozi.
        Assert.Equal(-3.862461647, corner.Longitude, 1e-7);
        Assert.Equal(43.167638726, corner.Latitude, 1e-5);
    }

    [Fact]
    public void The_short_lat_long_projection_of_the_ign_rmaps_is_accepted()
    {
        // The calibration of the IGN MTN25 sheet 0112 (10870 × 7374), scaled down to 300 × 200.
        var path = Rmap("0112.RMAP", 300, 200, """
            CompeGPS MAP File
            <Header>
            Version=2
            VerCompeGPS=7.0
            Projection=1,Lat/Long,
            Coordinates=1
            Datum=WGS 84
            </Header>
            <Map>
            Bitmap=0112.RMAP
            BitsPerPixel=0
            BitmapWidth=300
            BitmapHeight=200
            Type=10
            </Map>
            <Calibration>
            P0=0.00000000,0.00000000,A,-2.85416670,43.00000000
            P1=300.00000000,200.00000000,A,-2.52081880,42.83332450
            P2=0.00000000,200.00000000,A,-2.85416670,42.83332450
            P3=300.00000000,0.00000000,A,-2.52081880,43.00000000
            </Calibration>
            <MainPolygonBitmap>
            M0=0.0,0.0
            M1=300.0,0.0
            M2=300.0,200.0
            M3=0.0,200.0
            </MainPolygonBitmap>
            """, [new Level(300, 200, Red, Blue)]);

        var map = Load(path).Map;

        Assert.Equal(-2.8541667, map.ToWgs84(0, 0).Longitude, 1e-7);
        Assert.Equal(42.8333245, map.ToWgs84(300, 200).Latitude, 1e-7);
    }

    [Fact]
    public void A_missing_tile_leaves_a_transparent_hole()
    {
        var path = Rmap("hueco.rmap", 300, 200, Geographic, [new Level(300, 200, Red, null)]);

        var image = Load(path).Image;

        AssertPixel(Red, image, 10, 10);
        AssertPixel(null, image, 280, 100);
    }

    [Fact]
    public void An_rmap_with_other_tiles_than_jpeg_is_reported_as_unsupported()
    {
        var path = Rmap("paleta.rmap", 300, 200, Geographic, [new Level(300, 200, Red, Blue)], type: 4, subtype: 2);

        var ex = Assert.Throws<LocalMapException>(() => Load(path));

        Assert.Equal((LocalMapError.UnsupportedRmap, "tipo 4.2"), (ex.Error, ex.Detail));
    }

    [Fact]
    public void A_file_that_is_not_an_rmap_or_is_cut_short_is_rejected()
    {
        var other = Path.Combine(_folder, "otro.rmap");
        File.WriteAllText(other, "CompeGPS MAP File\r\nVersion=2\r\n");
        var whole = File.ReadAllBytes(Rmap("mapa.rmap", 300, 200, Geographic, [new Level(300, 200, Red, Blue)]));
        var cut = Path.Combine(_folder, "cortado.rmap");
        File.WriteAllBytes(cut, whole[..80]);

        Assert.Equal(LocalMapError.InvalidCalibration, Assert.Throws<LocalMapException>(() => Load(other)).Error);
        Assert.Equal(LocalMapError.InvalidCalibration, Assert.Throws<LocalMapException>(() => Load(cut)).Error);
    }

    [Fact]
    public void An_rmap_without_calibration_points_or_of_another_calibration_version_is_rejected()
    {
        var noPoints = Rmap("sin.rmap", 300, 200, "CompeGPS MAP File\nVersion=2\nDatum=WGS 84\n", [new Level(300, 200, Red, Blue)]);
        var version = Rmap("v3.rmap", 300, 200, Geographic.Replace("Version=2", "Version=3"), [new Level(300, 200, Red, Blue)]);

        Assert.Equal(LocalMapError.InvalidCalibration, Assert.Throws<LocalMapException>(() => Load(noPoints)).Error);
        var ex = Assert.Throws<LocalMapException>(() => Load(version));
        Assert.Equal((LocalMapError.InvalidCalibration, 3), (ex.Error, ex.Line));
    }

    [Fact]
    public void Rmap_is_among_the_local_cartography_extensions()
    {
        Assert.Contains(".rmap", LocalMapLoader.Extensions);
    }
}
