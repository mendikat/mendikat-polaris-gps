using System.Globalization;
using System.Text;
using Mendikat.TrailLab.Gpx;

namespace Mendikat.TrailLab.Tests.Gpx;

/// <summary>Opening large files reports progress and can be cancelled (NFR-02).</summary>
public sealed class LoadProgressTests
{
    private static MemoryStream LargeGpx(int points)
    {
        var builder = new StringBuilder("<gpx version=\"1.1\" xmlns=\"http://www.topografix.com/GPX/1/1\"><trk><trkseg>");
        for (var i = 0; i < points; i++)
        {
            builder.Append(CultureInfo.InvariantCulture, $"<trkpt lat=\"{42 + (i * 1e-6)}\" lon=\"-1.6\"><ele>500</ele></trkpt>");
        }

        builder.Append("</trkseg></trk></gpx>");
        return new MemoryStream(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    [Fact]
    public void Reading_reports_increasing_progress_from_0_to_1()
    {
        var reports = new List<double>();
        using var stream = LargeGpx(20_000);

        var data = new GpxReader().Read(stream, new SyncProgress(reports.Add), TestContext.Current.CancellationToken).Data;

        Assert.Equal(20_000, data.Tracks[0].PointCount);
        Assert.Equal(0, reports[0]);
        Assert.Equal(1, reports[^1]);
        Assert.True(reports.Count is > 10 and <= 102, $"{reports.Count} reports");
        Assert.Equal(reports.Order(), reports);
    }

    [Fact]
    public void Reading_can_be_cancelled_while_converting_points()
    {
        using var cancellation = new CancellationTokenSource();
        using var stream = LargeGpx(20_000);
        var progress = new SyncProgress(p =>
        {
            if (p > 0.5)
            {
                cancellation.Cancel();
            }
        });

        Assert.ThrowsAny<OperationCanceledException>(() => new GpxReader().Read(stream, progress, cancellation.Token));
    }

    [Fact]
    public void Reading_can_be_cancelled_while_parsing_the_xml()
    {
        using var cancellation = new CancellationTokenSource();
        using var stream = LargeGpx(20_000);
        var progress = new SyncProgress(p =>
        {
            if (p > 0.1)
            {
                cancellation.Cancel();
            }
        });

        Assert.ThrowsAny<OperationCanceledException>(() => new GpxReader().Read(stream, progress, cancellation.Token));
        Assert.True(stream.Position < stream.Length, "the file was read to the end");
    }

    /// <summary>Unlike <see cref="Progress{T}"/>, reports synchronously on the reading thread.</summary>
    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
