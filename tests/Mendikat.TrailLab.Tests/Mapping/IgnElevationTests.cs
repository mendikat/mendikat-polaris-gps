using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Mendikat.TrailLab.Domain.Elevation;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Mapping.Elevation;

namespace Mendikat.TrailLab.Tests.Mapping;

/// <summary>
/// IGN WCS elevation source against a fake server that reproduces the real responses observed on 2026-10-25:
/// multipart/related wrapping an ArcGrid without NODATA, zeros at sea/outside Spain, and ExtentError outside the coverage.
/// No real network access.
/// </summary>
public sealed partial class IgnElevationTests
{
    private const string RealResponse = """
        --wcs
        Content-Type: application/asc
        Content-Description: coverage data
        Content-Transfer-Encoding: binary
        Content-ID: coverage/out.asc
        Content-Disposition: INLINE; filename=out.asc

        ncols        3
        nrows        2
        xllcorner    -2.079000000000
        yllcorner    42.985000000000
        cellsize     0.001000000000
         841.804 841.780 841.492
         844.987 844.280 843.816
        --wcs--
        """;

    private const string ExtentError = """
        <?xml version="1.0" encoding="UTF-8"?>
        <ows:ExceptionReport xmlns:ows="http://www.opengis.net/ows/2.0" version="2.0.1">
          <ows:Exception exceptionCode="ExtentError" locator="extent"><ows:ExceptionText>Image extent does not intersect with desired region.</ows:ExceptionText></ows:Exception>
        </ows:ExceptionReport>
        """;

    /// <summary>Terrain used by the fake server: a plane, so bilinear interpolation must reproduce it exactly.</summary>
    private static double Terrain(double lat, double lon) => 1000 * (lat - 42) + 500 * (lon + 2);

    [Fact]
    public void ArcGrid_is_read_from_the_multipart_response()
    {
        var grid = ArcGrid.Parse(RealResponse);

        Assert.Equal((3, 2), (grid.Columns, grid.Rows));
        Assert.Equal((-2.079, 42.985, 0.001), (grid.West, grid.South, grid.CellSize));
        Assert.Equal(841.804, grid[0, 0]);  // first row is the northernmost
        Assert.Equal(843.816, grid[1, 2]);
        Assert.Null(grid.NoData);
    }

    [Fact]
    public void Declared_nodata_and_invalid_grids_are_handled()
    {
        var grid = ArcGrid.Parse("ncols 2\nnrows 2\nxllcorner 0\nyllcorner 0\ncellsize 1\nNODATA_value -9999\n1 2\n-9999 4\n");
        Assert.Equal(-9999, grid.NoData);
        Assert.Null(grid.Interpolate(1, 1, _ => false)); // one of the four cells is NODATA

        Assert.Throws<FormatException>(() => ArcGrid.Parse("<html>error</html>"));
        Assert.Throws<FormatException>(() => ArcGrid.Parse("ncols 2\nnrows 2\nxllcorner 0\nyllcorner 0\ncellsize 1\n1 2 3\n"));
    }

    [Fact]
    public void Bilinear_interpolation_uses_cell_centres()
    {
        // 2 x 2 grid, cells of 1°: centres at (lat 1.5, lon 0.5)=10, (1.5, 1.5)=20, (0.5, 0.5)=30, (0.5, 1.5)=40.
        var grid = ArcGrid.Parse("ncols 2\nnrows 2\nxllcorner 0\nyllcorner 0\ncellsize 1\n10 20\n30 40\n");

        Assert.Equal(10, grid.Interpolate(1.5, 0.5, _ => false));
        Assert.Equal(25, grid.Interpolate(1.0, 1.0, _ => false));
        Assert.Equal(15, grid.Interpolate(1.5, 1.0, _ => false));
        Assert.Null(grid.Interpolate(5, 5, _ => false)); // outside
    }

    [Fact]
    public void Exact_zeros_mean_no_data_because_the_service_has_no_nodata_value()
    {
        var grid = ArcGrid.Parse("ncols 2\nnrows 2\nxllcorner 0\nyllcorner 0\ncellsize 1\n0 20\n30 40\n");

        Assert.True(IgnWcsElevationSource.IsNoData(0));
        Assert.Null(grid.Interpolate(1, 1, IgnWcsElevationSource.IsNoData));
        Assert.Equal(40, grid.Interpolate(0.5, 1.5, IgnWcsElevationSource.IsNoData));
    }

    [Fact]
    public void Request_url_asks_for_an_ascii_grid_of_a_small_tile_with_margin()
    {
        var source = new IgnWcsElevationSource(new HttpClient(), IgnDemResolution.FiveMeters);

        var url = source.TileUrl(8597, -416).ToString();

        Assert.StartsWith("https://servicios.idee.es/wcs-inspire/mdt?SERVICE=WCS&VERSION=2.0.1&REQUEST=GetCoverage&COVERAGEID=Elevacion4258_5", url);
        Assert.Contains("&SUBSET=Lat(42.9849,42.9901)&SUBSET=Long(-2.0801,-2.0749)&FORMAT=application/asc", url);
    }

    [Fact]
    public async Task Elevations_are_interpolated_with_one_request_per_tile()
    {
        var server = new FakeWcs();
        using var http = new HttpClient(server);
        var source = new IgnWcsElevationSource(http);
        GeoPosition[] positions = [new(42.98531, -2.07862), new(42.98532, -2.07851), new(42.99512, -2.06103)];
        var progress = new List<double>();

        var values = await source.GetElevationsAsync(positions, new SyncProgress(progress.Add), TestContext.Current.CancellationToken);

        Assert.Equal(2, server.Requests); // the first two points share a tile
        for (var i = 0; i < positions.Length; i++)
        {
            Assert.Equal(Terrain(positions[i].Latitude, positions[i].Longitude), values[i]!.Value, 6);
        }

        Assert.Equal(1.0, progress[^1]);
    }

    [Fact]
    public async Task Downloaded_tiles_are_reused()
    {
        var server = new FakeWcs();
        using var http = new HttpClient(server);
        var source = new IgnWcsElevationSource(http);

        var first = await source.GetElevationsAsync([new(42.98531, -2.07862)], null, TestContext.Current.CancellationToken);
        var second = await source.GetElevationsAsync([new(42.98601, -2.07701)], null, TestContext.Current.CancellationToken);

        Assert.Equal(1, server.Requests); // same 0.005° tile
        Assert.Equal(Terrain(42.98601, -2.07701), second[0]!.Value, 6);
        Assert.NotNull(first[0]);
    }

    [Fact]
    public async Task Sea_and_outside_the_coverage_give_no_data()
    {
        using var http = new HttpClient(new FakeWcs { ZeroArea = lat => lat > 43.5, OutsideExtent = lat => lat > 45 });
        var source = new IgnWcsElevationSource(http);

        var values = await source.GetElevationsAsync(
            [new(42.9, -2), new(43.6, -2.5), new(48, 2)], null, TestContext.Current.CancellationToken);

        Assert.NotNull(values[0]);
        Assert.Null(values[1]); // zeros (sea)
        Assert.Null(values[2]); // ExtentError
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Service_errors_raise_an_elevation_source_exception(HttpStatusCode status)
    {
        using var http = new HttpClient(new FakeWcs { Status = status });

        await Assert.ThrowsAsync<ElevationSourceException>(() =>
            new IgnWcsElevationSource(http).GetElevationsAsync([new(42.9, -2)], null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Network_failures_and_cancellation_are_reported()
    {
        using var offline = new HttpClient(new FakeWcs { Fail = true });
        await Assert.ThrowsAsync<ElevationSourceException>(() =>
            new IgnWcsElevationSource(offline).GetElevationsAsync([new(42.9, -2)], null, TestContext.Current.CancellationToken));

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        using var http = new HttpClient(new FakeWcs());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new IgnWcsElevationSource(http).GetElevationsAsync([new(42.9, -2)], null, cancellation.Token));
    }

    [GeneratedRegex(@"SUBSET=Lat\(([-\d.]+),([-\d.]+)\)&SUBSET=Long\(([-\d.]+),([-\d.]+)\)")]
    private static partial Regex Subset();

    /// <summary>Reports synchronously (System.Progress posts to a synchronization context).</summary>
    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }

    /// <summary>Answers GetCoverage like the IGN service, with values from <see cref="Terrain"/>.</summary>
    private sealed class FakeWcs : HttpMessageHandler
    {
        private int _requests;

        public int Requests => _requests;

        public Func<double, bool> ZeroArea { get; init; } = _ => false;

        public Func<double, bool> OutsideExtent { get; init; } = _ => false;

        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;

        public bool Fail { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _requests);
            if (Fail)
            {
                throw new HttpRequestException("offline");
            }

            if (Status != HttpStatusCode.OK)
            {
                return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent("<html>error</html>") });
            }

            var m = Subset().Match(Uri.UnescapeDataString(request.RequestUri!.Query));
            double south = D(m.Groups[1].Value), north = D(m.Groups[2].Value), west = D(m.Groups[3].Value), east = D(m.Groups[4].Value);
            if (OutsideExtent(south))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(ExtentError) });
            }

            const double cell = 0.0001;
            var columns = (int)Math.Round((east - west) / cell);
            var rows = (int)Math.Round((north - south) / cell);
            var body = new StringBuilder("--wcs\r\nContent-Type: application/asc\r\n\r\n");
            body.Append(CultureInfo.InvariantCulture, $"ncols {columns}\nnrows {rows}\nxllcorner {west}\nyllcorner {south}\ncellsize {cell}\n");
            for (var r = 0; r < rows; r++)
            {
                var lat = north - (r + 0.5) * cell;
                for (var c = 0; c < columns; c++)
                {
                    var lon = west + (c + 0.5) * cell;
                    body.Append(CultureInfo.InvariantCulture, $" {(ZeroArea(lat) ? 0 : Terrain(lat, lon)):0.000000}");
                }

                body.Append('\n');
            }

            body.Append("--wcs--\r\n");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body.ToString()) });
        }

        private static double D(string text) => double.Parse(text, CultureInfo.InvariantCulture);
    }
}
