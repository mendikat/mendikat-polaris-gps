using Mendikat.TrailLab.App.Map;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Mapping.Coordinates;
using Mendikat.TrailLab.Mapping.Local;

namespace Mendikat.TrailLab.Tests.Mapping;

/// <summary>
/// OziExplorer .map and CompeGPS .imp calibrations and their drawing (LMAP-02..04, 06, 09). The reference files are
/// written to a temporary folder: PNG images with known colours and calibrations with known coordinates.
/// </summary>
public sealed class LocalMapReaderTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "traillab-local-" + Guid.NewGuid().ToString("N")[..8]);

    public LocalMapReaderTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>A PNG whose left half is red and right half blue.</summary>
    internal static byte[] HalfRedHalfBluePng(int width, int height)
    {
        var rgba = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = ((y * width) + x) * 4;
                rgba[i] = (byte)(x < width / 2 ? 255 : 0);
                rgba[i + 2] = (byte)(x < width / 2 ? 0 : 255);
                rgba[i + 3] = 255;
            }
        }

        return PngEncoder.Encode(width, height, rgba);
    }

    private string Write(string name, string text)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, text.ReplaceLineEndings("\r\n"), System.Text.Encoding.Latin1);
        return path;
    }

    private string Image(string name = "mapa.png", int width = 200, int height = 100)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, HalfRedHalfBluePng(width, height));
        return path;
    }

    /// <summary>An OziExplorer 2.2 file; <paramref name="points"/> are complete "PointNN" lines.</summary>
    private string OziMap(string datum, string projection, string image, params string[] points) => Write("mapa.map", $"""
        OziExplorer Map Data File Version 2.2
        Mapa de prueba
        {image}
        1 ,Map Code,
        {datum},{datum},   0.0000,   0.0000,{datum}
        Reserved 1
        Reserved 2
        Magnetic Variation,,,E
        Map Projection,{projection},PolyCal,No,AutoCalOnly,No,BSBUseWPX,No
        {string.Join("\n", points)}
        Point05,xy,     ,     ,in, deg,    ,        ,N,    ,        ,W, grid,   ,           ,           ,N
        Projection Setup,,,,,,,,,,
        Map Feature = MF ; Map Comment = MC     These follow if map feature or comments exist
        MM0,Yes
        IWH,Map Image Width/Height,200,100
        """);

    private static string Grid(int n, int px, int py, int zone, double e, double north) =>
        $"Point0{n},xy,{px,5},{py,5},in, deg,    ,        ,N,    ,        ,W, grid, {zone},{e,11:0},{north,11:0},N";

    private static LocalMapContent Load(string path) => LocalMapLoader.Load(path, new WpfRasterImageDecoder(), TestContext.Current.CancellationToken);

    private static GeoreferencedMap LoadMap(string path) => Assert.IsType<LocalRasterMap>(Load(path)).Map;

    private static void AssertNear(GeoPosition expected, GeoPosition actual, double tolerance = 1e-7)
    {
        Assert.Equal(expected.Latitude, actual.Latitude, tolerance);
        Assert.Equal(expected.Longitude, actual.Longitude, tolerance);
    }

    [Fact]
    public void Ozi_utm_calibration_points_land_where_they_say()
    {
        Image();
        var path = OziMap("WGS 84", "UTM Grid", "mapa.png",
            Grid(1, 0, 0, 30, 600000, 4750000), Grid(2, 200, 0, 30, 602000, 4750000),
            Grid(3, 0, 100, 30, 600000, 4749000), Grid(4, 200, 100, 30, 602000, 4749000));
        var utm = Crs.Utm(30, true, MapDatum.Wgs84);

        var map = LoadMap(path);

        Assert.Equal(("OziExplorer", 200, 100, 4), (map.Calibration.Format, map.ImageWidth, map.ImageHeight, map.Calibration.Points.Count));
        Assert.True(map.Transform.MaxResidualPixels < 1e-6);
        AssertNear(utm.ToWgs84(600000, 4750000), map.ToWgs84(0, 0));
        AssertNear(utm.ToWgs84(601000, 4749500), map.ToWgs84(100, 50));
        var (px, py) = map.ToPixel(utm.ToWgs84(602000, 4749000));
        Assert.Equal(200, px, 1e-6);
        Assert.Equal(100, py, 1e-6);
    }

    [Fact]
    public void Ozi_ed50_map_is_placed_with_the_mendikat_net_transformation()
    {
        Image();
        var path = OziMap("European 1950", "UTM Grid", "mapa.png",
            Grid(1, 0, 0, 30, 430000, 4780000), Grid(2, 200, 100, 30, 432000, 4779000));

        var map = LoadMap(path);

        // Same point as LocalCrsTests: mendikat.net gives 43.167638726, -3.862461647 (latitude within 1 m, D12).
        var corner = map.ToWgs84(0, 0);
        Assert.Equal(-3.862461647, corner.Longitude, 1e-7);
        Assert.Equal(43.167638726, corner.Latitude, 1e-5);
        Assert.Equal(MapDatum.Ed50, map.Crs.Datum);
    }

    [Fact]
    public void Ozi_latitude_longitude_points_in_degrees_and_minutes()
    {
        Image();
        var path = OziMap("WGS 84", "Latitude/Longitude", "mapa.png",
            "Point01,xy,    0,    0,in, deg,  43,  0.0000,N,   2,  0.0000,W, grid,   ,           ,           ,N",
            "Point02,xy,  200,  100,in, deg,  42, 30.0000,N,   1, 30.0000,W, grid,   ,           ,           ,N");

        var map = LoadMap(path);

        AssertNear(new GeoPosition(42.75, -1.75), map.ToWgs84(100, 50));
        Assert.True(map.Crs.IsGeographic);
    }

    [Fact]
    public void Without_points_the_wgs84_corners_are_used()
    {
        Image();
        var path = Write("esquinas.map", """
            OziExplorer Map Data File Version 2.2
            Esquinas
            mapa.png
            1 ,Map Code,
            WGS 84,WGS 84,   0.0000,   0.0000,WGS 84
            Reserved 1
            Reserved 2
            Magnetic Variation,,,E
            Map Projection,Latitude/Longitude,PolyCal,No,AutoCalOnly,No,BSBUseWPX,No
            Point01,xy,     ,     ,in, deg,    ,        ,N,    ,        ,W, grid,   ,           ,           ,N
            MMPNUM,4
            MMPXY,1,0,0
            MMPXY,2,200,0
            MMPXY,3,200,100
            MMPXY,4,0,100
            MMPLL,1,  -2.000000,  43.000000
            MMPLL,2,  -1.000000,  43.000000
            MMPLL,3,  -1.000000,  42.500000
            MMPLL,4,  -2.000000,  42.500000
            """);

        AssertNear(new GeoPosition(42.75, -1.5), LoadMap(path).ToWgs84(100, 50));
    }

    [Fact]
    public void An_image_path_from_another_computer_is_found_next_to_the_map()
    {
        Image("hoja.png");
        var path = OziMap("WGS 84", "UTM Grid", @"D:\Mapas\otro equipo\hoja.png",
            Grid(1, 0, 0, 30, 600000, 4750000), Grid(2, 200, 100, 30, 602000, 4749000));

        Assert.Equal(Path.Combine(_folder, "hoja.png"), LoadMap(path).Calibration.ImagePath);
    }

    [Theory]
    [InlineData("falta.png", LocalMapError.ImageMissing, "falta.png")]
    [InlineData("mapa.ozf2", LocalMapError.UnsupportedImage, "mapa.ozf2")]
    [InlineData("mapa.ecw", LocalMapError.UnsupportedImage, "mapa.ecw")]
    public void Missing_or_unsupported_images_are_reported_by_name(string image, LocalMapError error, string detail)
    {
        var path = OziMap("WGS 84", "UTM Grid", image, Grid(1, 0, 0, 30, 600000, 4750000), Grid(2, 200, 100, 30, 602000, 4749000));

        var ex = Assert.Throws<LocalMapException>(() => Load(path));

        Assert.Equal((error, detail), (ex.Error, ex.Detail));
    }

    [Theory]
    [InlineData("Tokyo", "UTM Grid", LocalMapError.UnsupportedCrs)]
    [InlineData("WGS 84", "Lambert Conformal Conic", LocalMapError.UnsupportedCrs)]
    [InlineData("European 1950", "UTM Grid", LocalMapError.UnsupportedCrs)] // zone 33: no ED50 parameters
    public void Unsupported_datums_and_projections_are_rejected_instead_of_misplaced(string datum, string projection, LocalMapError error)
    {
        Image();
        var path = OziMap(datum, projection, "mapa.png", Grid(1, 0, 0, 33, 600000, 4750000), Grid(2, 200, 100, 33, 602000, 4749000));

        Assert.Equal(error, Assert.Throws<LocalMapException>(() => Load(path)).Error);
    }

    [Fact]
    public void Truncated_or_incomplete_calibrations_are_rejected()
    {
        Image();
        var truncated = Write("corto.map", "OziExplorer Map Data File Version 2.2\nMapa\n");
        var onePoint = OziMap("WGS 84", "UTM Grid", "mapa.png", Grid(1, 0, 0, 30, 600000, 4750000));
        var notOzi = Write("otro.map", "esto no es un mapa\n1\n2\n3\n4\n");

        Assert.Equal(LocalMapError.InvalidCalibration, Assert.Throws<LocalMapException>(() => Load(truncated)).Error);
        Assert.Equal(LocalMapError.InvalidCalibration, Assert.Throws<LocalMapException>(() => Load(onePoint)).Error);
        Assert.Equal(LocalMapError.InvalidCalibration, Assert.Throws<LocalMapException>(() => Load(notOzi)).Error);
    }

    [Theory]
    [InlineData("no-existe.map", LocalMapError.FileNotFound)]
    [InlineData("vacio.map", LocalMapError.EmptyFile)]
    [InlineData("mapa.sid", LocalMapError.UnsupportedFormat)]
    [InlineData("roto.ecw", LocalMapError.ReadFailed)]
    [InlineData("roto.tif", LocalMapError.ReadFailed)]
    public void Missing_empty_and_unknown_files_give_clear_errors(string name, LocalMapError error)
    {
        File.WriteAllBytes(Path.Combine(_folder, "vacio.map"), []);
        File.WriteAllBytes(Path.Combine(_folder, "mapa.sid"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(_folder, "roto.ecw"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(_folder, "roto.tif"), [1, 2, 3]);

        Assert.Equal(error, Assert.Throws<LocalMapException>(() => Load(Path.Combine(_folder, name))).Error);
    }

    [Fact]
    public void Compegps_imp_utm_ed50_gives_the_same_placement_as_ozi()
    {
        Image();
        var path = Write("mapa.imp", """
            CompeGPS MAP File
            <Header>
            Version=2
            VerCompeGPS=7.6.0
            Projection=2,UTM,30,N,
            Coordinates=1
            Datum=European 1950
            </Header>
            <Map>
            Bitmap=C:\mapas\mapa.png
            BitsPerPixel=0
            BitmapWidth=200
            BitmapHeight=100
            Type=10
            </Map>
            <Calibration>
            P0=0.00000000,0.00000000,30T,430000.00000000,4780000.00000000
            P1=200.00000000,0.00000000,30T,432000.00000000,4780000.00000000
            P2=200.00000000,100.00000000,30T,432000.00000000,4779000.00000000
            P3=0.00000000,100.00000000,30T,430000.00000000,4779000.00000000
            </Calibration>
            """);

        var map = LoadMap(path);

        Assert.Equal("CompeGPS", map.Calibration.Format);
        Assert.Equal(-3.862461647, map.ToWgs84(0, 0).Longitude, 1e-7);
        Assert.Equal(43.167638726, map.ToWgs84(0, 0).Latitude, 1e-5);
    }

    [Fact]
    public void Compegps_imp_with_longitude_latitude_points()
    {
        Image();
        var path = Write("geo.imp", """
            CompeGPS MAP File
            <Header>
            Projection=1,Latitude/Longitude,
            Datum=WGS 84
            </Header>
            <Map>
            Bitmap=mapa.png
            </Map>
            <Calibration>
            P0=0,0,A,-2.0,43.0
            P1=200,100,A,-1.0,42.5
            </Calibration>
            """);

        AssertNear(new GeoPosition(42.75, -1.5), LoadMap(path).ToWgs84(100, 50));
    }

    [Fact]
    public void An_imp_without_bitmap_or_with_a_missing_image_is_rejected()
    {
        var noBitmap = Write("sin.imp", "CompeGPS MAP File\n<Calibration>\nP0=0,0,A,-2,43\n</Calibration>\n");
        var missing = Write("falta.imp", "CompeGPS MAP File\n<Map>\nBitmap=no-esta.jpg\n</Map>\n<Calibration>\nP0=0,0,A,-2,43\nP1=10,10,A,-1,42\n</Calibration>\n");

        Assert.Equal(LocalMapError.InvalidCalibration, Assert.Throws<LocalMapException>(() => Load(noBitmap)).Error);
        var ex = Assert.Throws<LocalMapException>(() => Load(missing));
        Assert.Equal((LocalMapError.ImageMissing, "no-esta.jpg"), (ex.Error, ex.Detail));
    }

    [Fact]
    public void Tiles_show_the_image_where_it_is_calibrated_and_nothing_outside()
    {
        Image(); // red west half, blue east half
        var path = OziMap("WGS 84", "Latitude/Longitude", "mapa.png",
            "Point01,xy,    0,    0,in, deg,  43,  0.0000,N,   2,  0.0000,W, grid,   ,           ,           ,N",
            "Point02,xy,  200,  100,in, deg,  42,  0.0000,N,   1,  0.0000,W, grid,   ,           ,           ,N");
        var raster = Assert.IsType<LocalRasterMap>(Load(path));
        var source = new LocalMapTileSource(raster.Map, raster.Image, "mapa");

        // A tile of about 1.1 km around a point, decoded back to read its centre pixel.
        (byte R, byte G, byte B, byte A) Centre(double lat, double lon)
        {
            var (x, y) = WebMercator.ToWorld(new GeoPosition(lat, lon));
            var png = source.Render(x - 600, y - 600, x + 600, y + 600);
            if (png is null)
            {
                return (0, 0, 0, 0);
            }

            var frame = System.Windows.Media.Imaging.BitmapDecoder.Create(new MemoryStream(png), default, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad).Frames[0];
            var bgra = new byte[4];
            new System.Windows.Media.Imaging.FormatConvertedBitmap(frame, System.Windows.Media.PixelFormats.Bgra32, null, 0)
                .CopyPixels(new System.Windows.Int32Rect(128, 128, 1, 1), bgra, 4, 0);
            return (bgra[2], bgra[1], bgra[0], bgra[3]);
        }

        Assert.Equal((255, 0, 0, 255), Centre(42.5, -1.75));
        Assert.Equal((0, 0, 255, 255), Centre(42.5, -1.25));
        Assert.Equal((0, 0, 0, 0), Centre(44, -1.5)); // outside: no tile at all
    }

    [Fact]
    public void Large_images_are_decoded_at_a_reduced_size()
    {
        var path = Image("grande.png", 400, 200);

        var image = new WpfRasterImageDecoder().Decode(path, maxPixels: 20_000, TestContext.Current.CancellationToken);

        Assert.Equal(200, image.Width);
        Assert.Equal(100, image.Height);
        Assert.Equal(0.5, image.Scale, 3);
    }

    [Fact]
    public void A_calibration_with_two_points_in_the_same_row_is_rejected()
    {
        Image();
        var path = OziMap("WGS 84", "UTM Grid", "mapa.png", Grid(1, 0, 0, 30, 600000, 4750000), Grid(2, 200, 0, 30, 602000, 4750000));

        Assert.Equal(LocalMapError.InvalidCalibration, Assert.Throws<LocalMapException>(() => Load(path)).Error);
    }
}
