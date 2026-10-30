using System.Buffers.Binary;
using Polaris.Gps.App.Map;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Mapping.Local;

namespace Polaris.Gps.Tests.Mapping;

/// <summary>Local DEMs: SRTM .hgt and ESRI ASCII grid .asc with .prj (LDEM-01, 02, 04..06).</summary>
public sealed class LocalDemTests : IDisposable
{
    /// <summary>ESRI WKT of ETRS89 / UTM zone 30N, as written by ArcGIS and QGIS.</summary>
    internal const string EtrsUtm30Prj =
        "PROJCS[\"ETRS_1989_UTM_Zone_30N\",GEOGCS[\"GCS_ETRS_1989\",DATUM[\"D_ETRS_1989\",SPHEROID[\"GRS_1980\",6378137.0,298.257222101]]," +
        "PRIMEM[\"Greenwich\",0.0],UNIT[\"Degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"False_Easting\",500000.0]," +
        "PARAMETER[\"False_Northing\",0.0],PARAMETER[\"Central_Meridian\",-3.0],PARAMETER[\"Scale_Factor\",0.9996]," +
        "PARAMETER[\"Latitude_Of_Origin\",0.0],UNIT[\"Meter\",1.0]]";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "polarisgps-dem-" + Guid.NewGuid().ToString("N")[..8]);

    public LocalDemTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static LocalMapContent Load(string path) => LocalMapLoader.Load(path, new WpfRasterImageDecoder(), TestContext.Current.CancellationToken);

    private static ElevationGrid LoadGrid(string path) => Assert.IsType<ElevationGrid>(Assert.IsType<LocalDem>(Load(path)).Dem);

    /// <summary>A 3″ HGT whose value at row r, column c is r + c, with a void at row 10, column 10.</summary>
    private string Hgt(string name = "N42W002.hgt", int size = 1201)
    {
        var bytes = new byte[size * size * 2];
        for (var r = 0; r < size; r++)
        {
            for (var c = 0; c < size; c++)
            {
                var value = (short)(r == 10 && c == 10 ? HgtReader.Void : r + c);
                BinaryPrimitives.WriteInt16BigEndian(bytes.AsSpan(((r * size) + c) * 2), value);
            }
        }

        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private string Asc(string name, string header, string values, string? prj = EtrsUtm30Prj)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, header + values);
        if (prj is not null)
        {
            File.WriteAllText(Path.ChangeExtension(path, ".prj"), prj);
        }

        return path;
    }

    /// <summary>3 × 3 grid: 100 110 120 / 200 210 220 / 300 310 -9999 (north to south).</summary>
    private const string Values = "100 110 120\n200 210 220\n300 310 -9999\n";

    [Fact]
    public void Hgt_nodes_and_interpolation_from_the_file_name_corner()
    {
        var grid = LoadGrid(Hgt());

        Assert.Equal((1201, 1201), (grid.Columns, grid.Rows));
        Assert.Equal(new GeoBounds(42, -2, 43, -1), grid.Bounds);
        Assert.Equal(0, grid.ElevationAt(new GeoPosition(43, -2))); // north-west node: row 0, column 0
        Assert.Equal(1200 + 1200, grid.ElevationAt(new GeoPosition(42, -1))!.Value, 6); // south-east node
        Assert.Equal(100 + 50, grid.ElevationAt(new GeoPosition(43 - (100.0 / 1200), -2 + (50.0 / 1200)))!.Value, 6);
        Assert.Equal(100.5 + 50, grid.ElevationAt(new GeoPosition(43 - (100.5 / 1200), -2 + (50.0 / 1200)))!.Value, 6);
    }

    [Fact]
    public void Hgt_voids_and_positions_outside_the_tile_have_no_data()
    {
        var grid = LoadGrid(Hgt());

        Assert.Null(grid.ElevationAt(new GeoPosition(43 - (10.0 / 1200), -2 + (10.0 / 1200)))); // the void
        Assert.Null(grid.ElevationAt(new GeoPosition(43 - (10.5 / 1200), -2 + (10.5 / 1200)))); // next to it
        Assert.Null(grid.ElevationAt(new GeoPosition(43.5, -1.5)));
        Assert.NotNull(grid.ElevationAt(new GeoPosition(42.5, -1.5)));
    }

    [Theory]
    [InlineData("N42W002.hgt", 1000, LocalMapError.InvalidDem)]  // truncated
    [InlineData("mapa.hgt", 1201, LocalMapError.InvalidDem)]     // no corner in the name
    public void Invalid_hgt_files_are_rejected(string name, int size, LocalMapError error)
    {
        var path = Hgt(name, size);

        Assert.Equal(error, Assert.Throws<LocalMapException>(() => Load(path)).Error);
    }

    [Fact]
    public void Ascii_grid_with_corner_and_with_center_gives_the_same_values()
    {
        var corner = LoadGrid(Asc("esquina.asc", "ncols 3\nnrows 3\nxllcorner 599975\nyllcorner 4749975\ncellsize 50\nNODATA_value -9999\n", Values));
        var center = LoadGrid(Asc("centro.asc", "NCOLS 3\nNROWS 3\nXLLCENTER 600000\nYLLCENTER 4750000\nCELLSIZE 50\nNODATA_VALUE -9999\n", Values));
        var utm = Crs.Utm(30, true, MapDatum.Etrs89);

        foreach (var grid in new[] { corner, center })
        {
            Assert.Equal(100, grid.ElevationAt(utm.ToWgs84(600000, 4750100))!.Value, 3); // north-west node
            Assert.Equal(310, grid.ElevationAt(utm.ToWgs84(600050, 4750000))!.Value, 3);
            Assert.Equal(155, grid.ElevationAt(utm.ToWgs84(600025, 4750075))!.Value, 3); // between 100, 110, 200, 210
            Assert.Null(grid.ElevationAt(utm.ToWgs84(600100, 4750000))); // NODATA node
            Assert.Null(grid.ElevationAt(utm.ToWgs84(601000, 4750000))); // outside
        }
    }

    [Fact]
    public void Ascii_grid_reports_its_system_and_an_unknown_vertical_reference()
    {
        var grid = LoadGrid(Asc("mdt.asc", "ncols 3\nnrows 3\nxllcorner 599975\nyllcorner 4749975\ncellsize 50\n", Values));

        Assert.Equal("UTM 30N ETRS89", grid.Crs.Description);
        Assert.Contains("desconocida", grid.VerticalDatum, StringComparison.Ordinal);
        Assert.Equal(-9999, grid[2, 2]); // without NODATA_value, -9999 is a value
    }

    [Fact]
    public void Ascii_grid_without_prj_is_rejected_instead_of_guessed()
    {
        var path = Asc("sin.asc", "ncols 3\nnrows 3\nxllcorner 0\nyllcorner 0\ncellsize 1\n", Values, prj: null);

        var ex = Assert.Throws<LocalMapException>(() => Load(path));

        Assert.Equal((LocalMapError.MissingProjection, "sin.prj"), (ex.Error, ex.Detail));
    }

    [Theory]
    [InlineData("MDT05-0112-H30-LIDAR.asc", "MDT05-0112-H30-LIDAR.PRJ")]
    [InlineData("MDT05-0112-H30-LIDAR.ASC", "mdt05-0112-h30-lidar.prj")]
    [InlineData("mdt.asc", "mdt.asc.prj")]
    public void The_prj_is_found_whatever_its_case(string asc, string prj)
    {
        var path = Asc(asc, "ncols 3\nnrows 3\nxllcorner 599975\nyllcorner 4749975\ncellsize 50\n", Values, prj: null);
        File.WriteAllText(Path.Combine(_folder, prj), EtrsUtm30Prj);

        Assert.Equal("UTM 30N ETRS89", LoadGrid(path).Crs.Description);
    }

    /// <summary>The classic ArcInfo .prj distributed with the IGN ASCII grids.</summary>
    private const string ArcInfoPrj = "Projection    UTM\r\nZone          30\r\nDatum         WGS 84\r\nZunits        METERS\r\nUnits         METERS\r\n";

    [Theory]
    [InlineData(ArcInfoPrj, "UTM 30N WGS84")]
    [InlineData("Projection    UTM\nZone          28\nDatum         REGCAN95\nUnits         METERS\n", "UTM 28N REGCAN95")]
    [InlineData("Projection    UTM\nZone          -23\nDatum         WGS84\nUnits         METERS\n", "UTM 23S WGS84")]
    [InlineData("Projection    GEOGRAPHIC\nDatum         WGS84\nUnits         DD\n", "WGS84 geográficas")]
    public void Arcinfo_prj_files_are_recognised(string prj, string expected)
    {
        Assert.Equal(expected, PrjReader.Parse(prj).Description);
    }

    [Theory]
    [InlineData("MDT05-0112-H30-LIDAR", "UTM 30N ETRS89")]
    [InlineData("MDT05-0091B-0117-H30 ", "UTM 30N ETRS89")]
    [InlineData("MDT25-0023-H29", "UTM 29N ETRS89")]
    [InlineData("PNOA_MDT05_ETRS89_HU30_0112_LID", "UTM 30N ETRS89")]
    [InlineData("PNOA_MDT05_ETRS89_HU31_0420_LID (1)", "UTM 31N ETRS89")]
    [InlineData("PNOA_MDT05_REGCAN95_HU28_1096_LID", "UTM 28N REGCAN95")]
    [InlineData("mdt", null)]
    [InlineData("H30-recorte", null)]
    public void Ign_terrain_model_names_state_their_system(string name, string? expected)
    {
        Assert.Equal(expected, IgnName.Crs(name)?.Description);
    }

    [Fact]
    public void An_ign_grid_ending_with_ctrl_z_and_a_prj_named_with_a_space_is_read()
    {
        // As found in IGN downloads: "MDT05-0112-H30-LIDAR.asc" next to "MDT05-0112-H30-LIDAR .PRJ", file ending in ^Z.
        var path = Asc("MDT05-0112-H30-LIDAR.asc", "NCOLS 3\r\nNROWS 3\r\nXLLCENTER 600000\r\nYLLCENTER 4750000\r\nCELLSIZE 50\r\nNODATA_VALUE -999\r\n", Values.Replace("\n", " \r\n") + "\u001A", prj: null);
        File.WriteAllText(Path.Combine(_folder, "MDT05-0112-H30-LIDAR .PRJ"), ArcInfoPrj);

        var grid = LoadGrid(path);

        Assert.Equal("UTM 30N ETRS89", grid.Crs.Description);
        Assert.Equal(-9999, grid[2, 2]);
        Assert.Contains("REDNAP", grid.VerticalDatum, StringComparison.Ordinal);
        Assert.DoesNotContain("no se usa", grid.VerticalDatum, StringComparison.Ordinal); // WGS84 vs ETRS89: same system
    }

    [Fact]
    public void An_ign_name_wins_over_a_prj_with_another_zone_and_says_so()
    {
        // Real case: MDT05-0028-H29-LIDAR.asc (x from 727000, zone 29) shipped with a .prj saying zone 30.
        var path = Asc("MDT05-0028-H29-LIDAR.asc", "ncols 3\nnrows 3\nxllcenter 727000\nyllcenter 4801400\ncellsize 50\n", Values, prj: ArcInfoPrj);

        var grid = LoadGrid(path);

        Assert.Equal("UTM 29N ETRS89", grid.Crs.Description);
        Assert.Contains("el .prj dice UTM 30N WGS84 y no se usa", grid.VerticalDatum, StringComparison.Ordinal);
    }

    [Fact]
    public void An_ign_grid_without_prj_uses_its_name()
    {
        var path = Asc("MDT05-0023-H30-LIDAR.asc", "ncols 3\nnrows 3\nxllcenter 600000\nyllcenter 4750000\ncellsize 50\n", Values, prj: null);

        var grid = LoadGrid(path);

        Assert.Equal("UTM 30N ETRS89", grid.Crs.Description);
        Assert.Contains("Sistema según el nombre del IGN", grid.VerticalDatum, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("100 110 120\n200 210 220\n300 310\n")]       // one value missing
    [InlineData("100 110 120\n200 210 220\n300 310 320 330\n")] // one too many
    [InlineData("100 110 120\n200 x 220\n300 310 320\n")]       // not a number
    public void Ascii_grids_with_wrong_values_are_rejected(string values)
    {
        var path = Asc("mal.asc", "ncols 3\nnrows 3\nxllcorner 0\nyllcorner 0\ncellsize 1\n", values);

        Assert.Equal(LocalMapError.InvalidDem, Assert.Throws<LocalMapException>(() => Load(path)).Error);
    }

    [Theory]
    [InlineData(EtrsUtm30Prj, "UTM 30N ETRS89")]
    [InlineData("EPSG:23030", "UTM 30N ED50")]
    [InlineData("EPSG:4326", "WGS84 geográficas")]
    [InlineData("GEOGCS[\"GCS_WGS_1984\",DATUM[\"D_WGS_1984\",SPHEROID[\"WGS_1984\",6378137.0,298.257223563]],PRIMEM[\"Greenwich\",0.0],UNIT[\"Degree\",0.0174532925199433]]", "WGS84 geográficas")]
    public void Prj_files_are_recognised(string prj, string expected)
    {
        Assert.Equal(expected, PrjReader.Parse(prj).Description);
    }

    [Theory]
    [InlineData("EPSG:2154")]
    [InlineData("PROJCS[\"RGF93_Lambert_93\",GEOGCS[\"GCS_RGF_1993\",DATUM[\"D_RGF_1993\",SPHEROID[\"GRS_1980\",6378137.0,298.257222101]]],PROJECTION[\"Lambert_Conformal_Conic\"],UNIT[\"Meter\",1.0]]")]
    public void Unsupported_systems_are_rejected(string prj)
    {
        Assert.Throws<UnsupportedCrsException>(() => PrjReader.Parse(prj));
    }

    [Fact]
    public async Task A_dem_answers_elevation_queries_like_any_elevation_source()
    {
        Polaris.Gps.Domain.Elevation.IElevationSource source = LoadGrid(Hgt());

        var values = await source.GetElevationsAsync([new(43, -2), new(50, 5)], null, TestContext.Current.CancellationToken);

        Assert.Equal([0, null], values);
        Assert.Equal("N42W002.hgt", source.Name);
        Assert.Contains("EGM96", source.VerticalDatum, StringComparison.Ordinal);
    }
}
