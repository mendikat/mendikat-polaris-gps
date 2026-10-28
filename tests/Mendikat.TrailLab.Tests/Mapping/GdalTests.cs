using Mendikat.TrailLab.App.Map;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Mapping.Coordinates;
using Mendikat.TrailLab.Mapping.Local;
using OSGeo.GDAL;
using OSGeo.OSR;

namespace Mendikat.TrailLab.Tests.Mapping;

/// <summary>GeoTIFF maps and DEMs read with GDAL, on GeoTIFFs written by GDAL itself in a temporary folder.</summary>
public sealed class GdalTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "traillab-gdal-" + Guid.NewGuid().ToString("N")[..8]);

    public GdalTests()
    {
        Directory.CreateDirectory(_folder);
        GdalSetup.EnsureConfigured();
    }

    public void Dispose()
    {
        GC.Collect(); // GDAL handles of failed opens
        GC.WaitForPendingFinalizers();
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // a dataset still held by a finalizer: the temporary folder is left behind
        }
    }

    private static string Wkt(int epsg)
    {
        using var srs = new SpatialReference(string.Empty);
        srs.ImportFromEPSG(epsg);
        srs.ExportToWkt(out var wkt, null);
        return wkt;
    }

    /// <summary>
    /// A 200 × 100 RGB GeoTIFF: left half red, right half blue. <paramref name="transform"/> is the GDAL geotransform;
    /// without <paramref name="epsg"/> it has no coordinate system.
    /// </summary>
    private string RgbTiff(string name, double[] transform, int? epsg)
    {
        var path = Path.Combine(_folder, name);
        using (var dataset = Gdal.GetDriverByName("GTiff").Create(path, 200, 100, 3, DataType.GDT_Byte, null))
        {
            dataset.SetGeoTransform(transform);
            if (epsg is { } code)
            {
                dataset.SetProjection(Wkt(code));
            }

            for (var b = 1; b <= 3; b++)
            {
                var values = new byte[200 * 100];
                for (var i = 0; i < values.Length; i++)
                {
                    var left = i % 200 < 100;
                    values[i] = (byte)(b == 1 && left ? 255 : b == 3 && !left ? 255 : 0);
                }

                using var band = dataset.GetRasterBand(b);
                band.SetColorInterpretation(b switch { 1 => ColorInterp.GCI_RedBand, 2 => ColorInterp.GCI_GreenBand, _ => ColorInterp.GCI_BlueBand });
                band.WriteRaster(0, 0, 200, 100, values, 200, 100, 0, 0);
            }
        }

        return path;
    }

    /// <summary>
    /// A 3 × 3 Float32 DEM in ETRS89 UTM 30N, 50 m cells, top-left corner at (599975, 4750125): pixel centres at
    /// x 600000/600050/600100 and y 4750100/4750050/4750000. Values 100 110 120 / 200 210 220 / 300 310 NoData.
    /// </summary>
    private string DemTiff(string name = "mdt.tif")
    {
        var path = Path.Combine(_folder, name);
        using var dataset = Gdal.GetDriverByName("GTiff").Create(path, 3, 3, 1, DataType.GDT_Float32, null);
        dataset.SetGeoTransform([599975, 50, 0, 4750125, 0, -50]);
        dataset.SetProjection(Wkt(25830));
        using var band = dataset.GetRasterBand(1);
        band.SetNoDataValue(-9999);
        band.WriteRaster(0, 0, 3, 3, new float[] { 100, 110, 120, 200, 210, 220, 300, 310, -9999 }, 3, 3, 0, 0);
        return path;
    }

    private static LocalMapContent Load(string path) => LocalMapLoader.Load(path, new WpfRasterImageDecoder(), TestContext.Current.CancellationToken);

    /// <summary>Centre pixel (R, G, B, A) of a ~1.2 km tile around a position; transparent when there is no tile.</summary>
    private static (byte R, byte G, byte B, byte A) Centre(GdalMap map, double lat, double lon)
    {
        var (x, y) = WebMercator.ToWorld(new GeoPosition(lat, lon));
        var png = map.Render(x - 600, y - 600, x + 600, y + 600);
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

    [Fact]
    public void Gdal_and_its_ecw_plugin_are_available()
    {
        Assert.Null(GdalSetup.Problem);
        Assert.True(GdalSetup.HasEcw);
    }

    [Fact]
    public void A_geographic_geotiff_is_drawn_where_it_is_georeferenced()
    {
        // 200 × 100 pixels over lon −2..−1, lat 43..42.5 (0.005° pixels).
        using var content = Assert.IsType<LocalGdalMap>(Load(RgbTiff("geo.tif", [-2, 0.005, 0, 43, 0, -0.005], 4326)));

        Assert.Equal(-2, content.Bounds.MinLongitude, 6);
        Assert.Equal(42.5, content.Bounds.MinLatitude, 6);
        Assert.Equal((255, 0, 0, 255), Centre(content.Map, 42.75, -1.75));
        Assert.Equal((0, 0, 255, 255), Centre(content.Map, 42.75, -1.25));
        Assert.Equal((0, 0, 0, 0), Centre(content.Map, 44, -1.5));
        Assert.Equal("GTiff", content.Map.Raster.Driver);
    }

    [Fact]
    public void A_projected_geotiff_in_etrs89_utm_is_reprojected()
    {
        // 200 × 100 pixels of 10 m from (600000, 4750000), ETRS89 UTM 30N.
        using var content = Assert.IsType<LocalGdalMap>(Load(RgbTiff("utm.tif", [600000, 10, 0, 4750000, 0, -10], 25830)));
        var utm = Crs.Utm(30, true, MapDatum.Etrs89);
        var red = utm.ToWgs84(600500, 4749500);
        var blue = utm.ToWgs84(601500, 4749500);

        Assert.Equal((255, 0, 0, 255), Centre(content.Map, red.Latitude, red.Longitude));
        Assert.Equal((0, 0, 255, 255), Centre(content.Map, blue.Latitude, blue.Longitude));
        Assert.Contains("UTM zone 30N", content.Map.Raster.CrsDescription, StringComparison.Ordinal);
    }

    [Fact]
    public void A_cloud_optimized_geotiff_with_cog_extension_is_drawn()
    {
        var path = Path.Combine(_folder, "orto.cog");
        using (var source = Gdal.Open(RgbTiff("fuente.tif", [-2, 0.005, 0, 43, 0, -0.005], 4326), Access.GA_ReadOnly))
        using (Gdal.GetDriverByName("COG").CreateCopy(path, source, 0, ["OVERVIEWS=AUTO", "BLOCKSIZE=64"], null, null))
        {
        }

        using var content = Assert.IsType<LocalGdalMap>(Load(path));

        Assert.Equal((255, 0, 0, 255), Centre(content.Map, 42.75, -1.75));
        Assert.Equal((0, 0, 255, 255), Centre(content.Map, 42.75, -1.25));
    }

    [Fact]
    public void A_tiff_without_georeferencing_is_rejected()
    {
        var ex = Assert.Throws<LocalMapException>(() => Load(RgbTiff("foto.tif", [0, 1, 0, 0, 0, 1], null)));

        Assert.Equal((LocalMapError.NotGeoreferenced, "foto.tif"), (ex.Error, ex.Detail));
    }

    [Fact]
    public void A_one_band_float_geotiff_is_a_dem_with_values_and_nodata()
    {
        using var content = Assert.IsType<LocalDem>(Load(DemTiff()));
        var dem = Assert.IsType<GdalDem>(content.Dem);
        var utm = Crs.Utm(30, true, MapDatum.Etrs89);

        Assert.Equal(100, dem.ElevationAt(utm.ToWgs84(600000, 4750100))!.Value, 3); // centre of the top-left pixel
        Assert.Equal(155, dem.ElevationAt(utm.ToWgs84(600025, 4750075))!.Value, 3); // between 100, 110, 200, 210
        Assert.Equal(310, dem.ElevationAt(utm.ToWgs84(600050, 4750000))!.Value, 3); // next to the NoData pixel
        Assert.Null(dem.ElevationAt(utm.ToWgs84(600100, 4750000))); // NoData
        Assert.Null(dem.ElevationAt(utm.ToWgs84(601000, 4750000))); // outside
        Assert.Equal((3, 3), (dem.Columns, dem.Rows));
        Assert.Contains("desconocida", dem.VerticalDatum, StringComparison.Ordinal);
    }

    [Fact]
    public void A_geotiff_with_only_control_points_is_warped_and_placed()
    {
        var path = Path.Combine(_folder, "gcp.tif");
        using (var dataset = Gdal.GetDriverByName("GTiff").Create(path, 200, 100, 1, DataType.GDT_Byte, null))
        {
            GCP[] gcps =
            [
                new GCP(-2, 43, 0, 0, 0, "1", "1"),
                new GCP(-1, 43, 0, 200, 0, "2", "2"),
                new GCP(-2, 42.5, 0, 0, 100, "3", "3"),
                new GCP(-1, 42.5, 0, 200, 100, "4", "4"),
            ];
            dataset.SetGCPs(gcps, Wkt(4326));
            using var band = dataset.GetRasterBand(1);
            band.Fill(200, 0);
        }

        using var content = Assert.IsType<LocalGdalMap>(Load(path));

        Assert.Equal(42.5, content.Bounds.MinLatitude, 2);
        Assert.Equal(-1, content.Bounds.MaxLongitude, 2);
        Assert.Equal((200, 200, 200, 255), Centre(content.Map, 42.75, -1.5));
    }

    /// <summary>
    /// An image without georeferencing of its own (left half red, right half blue), placed by a world file: 20 × 10
    /// pixels of 10 m, the first pixel centred at (600005, 4750095).
    /// </summary>
    private string WorldFileImage(string image, string worldFile, string driver)
    {
        var path = Path.Combine(_folder, image);
        using (var memory = Gdal.GetDriverByName("MEM").Create(string.Empty, 20, 10, 3, DataType.GDT_Byte, null))
        {
            for (var b = 1; b <= 3; b++)
            {
                var values = Enumerable.Range(0, 200).Select(i => (byte)((b == 1 && i % 20 < 10) || (b == 3 && i % 20 >= 10) ? 255 : 0)).ToArray();
                using var band = memory.GetRasterBand(b);
                band.WriteRaster(0, 0, 20, 10, values, 20, 10, 0, 0);
            }

            using var copy = Gdal.GetDriverByName(driver).CreateCopy(path, memory, 0, null, null, null);
        }

        File.WriteAllText(Path.Combine(_folder, worldFile), "10\n0\n0\n-10\n600005\n4750095\n");
        return path;
    }

    private const string ArcInfoPrj = "Projection    UTM\r\nZone          30\r\nDatum         WGS 84\r\nZunits        METERS\r\nUnits         METERS\r\n";

    [Theory]
    [InlineData("hoja.tif", "hoja.tfw", "GTiff", "hoja.prj", ArcInfoPrj)]
    [InlineData("hoja.png", "hoja.pgw", "PNG", "HOJA .PRJ", ArcInfoPrj)]
    [InlineData("hoja.jpg", "hoja.jgw", "JPEG", "hoja.prj", "EPSG:32630")]
    [InlineData("hoja.tif", "hoja.wld", "GTiff", "hoja.tif.prj", "EPSG:32630")]
    public void An_image_with_a_world_file_and_a_prj_is_placed(string image, string worldFile, string driver, string prjName, string prj)
    {
        var path = WorldFileImage(image, worldFile, driver);
        File.WriteAllText(Path.Combine(_folder, prjName), prj);
        var utm = Crs.Utm(30, true, MapDatum.Wgs84);

        using var content = Assert.IsType<LocalGdalMap>(Load(path));

        var red = utm.ToWgs84(600050, 4750050);
        var blue = utm.ToWgs84(600150, 4750050);
        Assert.Equal(Math.Min(utm.ToWgs84(600000, 4750000).Latitude, utm.ToWgs84(600200, 4750000).Latitude), content.Bounds.MinLatitude, 6);
        var (r, _, _, a) = Centre(content.Map, red.Latitude, red.Longitude);
        var (_, _, bl, _) = Centre(content.Map, blue.Latitude, blue.Longitude);
        Assert.True(r > 200 && a == 255, $"red {r}"); // JPEG is lossy
        Assert.True(bl > 200, $"blue {bl}");
    }

    [Fact]
    public void Choosing_the_world_file_opens_its_image()
    {
        WorldFileImage("hoja.tif", "hoja.tfw", "GTiff");
        File.WriteAllText(Path.Combine(_folder, "hoja.prj"), ArcInfoPrj);

        using var content = Assert.IsType<LocalGdalMap>(Load(Path.Combine(_folder, "hoja.tfw")));

        Assert.EndsWith("hoja.tif", content.Map.Raster.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void A_world_file_without_prj_is_not_guessed()
    {
        var path = WorldFileImage("hoja.png", "hoja.pgw", "PNG");

        var ex = Assert.Throws<LocalMapException>(() => Load(path));

        Assert.Equal((LocalMapError.MissingProjection, "hoja.prj"), (ex.Error, ex.Detail));
    }

    [Fact]
    public void A_photo_without_world_file_is_not_georeferenced()
    {
        var path = Path.Combine(_folder, "foto.jpg");
        using (var memory = Gdal.GetDriverByName("MEM").Create(string.Empty, 4, 4, 3, DataType.GDT_Byte, null))
        using (Gdal.GetDriverByName("JPEG").CreateCopy(path, memory, 0, null, null, null))
        {
        }

        Assert.Equal(LocalMapError.NotGeoreferenced, Assert.Throws<LocalMapException>(() => Load(path)).Error);
    }

    [Fact]
    public void A_removed_map_draws_nothing_and_answers_nothing()
    {
        var content = Assert.IsType<LocalGdalMap>(Load(RgbTiff("geo.tif", [-2, 0.005, 0, 43, 0, -0.005], 4326)));
        var dem = Assert.IsType<LocalDem>(Load(DemTiff()));

        content.Dispose();
        dem.Dispose();

        Assert.Equal((0, 0, 0, 0), Centre(content.Map, 42.75, -1.75));
        Assert.Null(dem.Dem.ElevationAt(Crs.Utm(30, true, MapDatum.Etrs89).ToWgs84(600000, 4750100)));
    }
}
