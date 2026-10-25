using Mendikat.TrailLab.App.Services;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Gpx;

namespace Mendikat.TrailLab.Tests.Gpx;

public sealed class SafeSaveTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("traillab-save-").FullName;

    public void Dispose()
    {
        foreach (var file in Directory.GetFiles(_directory))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_directory, recursive: true);
    }

    private string PathOf(string name) => Path.Combine(_directory, name);

    [Fact]
    [Trait("Req", "SAVE-03")]
    public void Writing_creates_or_replaces_the_file_and_leaves_no_temporary_files()
    {
        var path = PathOf("a.gpx");

        SafeFileWriter.Write(path, [1, 2, 3]);
        SafeFileWriter.Write(path, [4, 5]);

        Assert.Equal([4, 5], File.ReadAllBytes(path));
        Assert.Equal(["a.gpx"], Directory.GetFiles(_directory).Select(Path.GetFileName));
    }

    [Fact]
    [Trait("Req", "SAVE-03")]
    public void A_failed_replace_keeps_the_original_intact_and_removes_the_temporary_file()
    {
        var path = PathOf("locked.gpx");
        File.WriteAllBytes(path, [9, 9, 9]);
        File.SetAttributes(path, FileAttributes.ReadOnly);

        Assert.ThrowsAny<Exception>(() => SafeFileWriter.Write(path, [1]));

        Assert.Equal([9, 9, 9], File.ReadAllBytes(path));
        Assert.Equal(["locked.gpx"], Directory.GetFiles(_directory).Select(Path.GetFileName));
    }

    [Fact]
    [Trait("Req", "SAVE-03")]
    public void Verification_rejects_output_that_does_not_match_the_document()
    {
        var data = new Mendikat.TrailLab.Domain.Model.TrailData();
        data.Waypoints.Add(new Mendikat.TrailLab.Domain.Model.TrailPoint(new GeoPosition(42, -1)));
        var shape = TrailFileWriter.Shape(data);
        var bytes = TrailFileWriter.Serialize(data, DocumentFormat.Gpx);

        Assert.True(TrailFileWriter.Verify(bytes, DocumentFormat.Gpx, shape));
        Assert.False(TrailFileWriter.Verify(bytes[..^10], DocumentFormat.Gpx, shape)); // truncated
        data.Waypoints[0].Position = new GeoPosition(43, -1);
        Assert.False(TrailFileWriter.Verify(bytes, DocumentFormat.Gpx, TrailFileWriter.Shape(data)));
    }

    [Theory]
    [Trait("Req", "SAVE-01")]
    [Trait("Req", "GPX-03")]
    [InlineData("gpx/valid-11.gpx", DocumentFormat.Gpx)]
    [InlineData("wpt/ozi-sample.wpt", DocumentFormat.Wpt)]
    [InlineData("wpt/compegps-ansi.wpt", DocumentFormat.Wpt)]
    public async Task Store_saves_in_the_same_format_and_the_file_reopens_identically(string fixture, DocumentFormat format)
    {
        var store = new FileDocumentStore();
        var document = await store.LoadAsync(Fixture.PathOf(fixture), format, TestContext.Current.CancellationToken);
        var target = PathOf("copy" + Path.GetExtension(fixture));

        await store.SaveAsync(document, target, TestContext.Current.CancellationToken);
        var reopened = await store.LoadAsync(target, format, TestContext.Current.CancellationToken);

        Assert.Equal(TrailFileWriter.Shape(document.Data), TrailFileWriter.Shape(reopened.Data), ShapeComparer.Instance);
        Assert.Equal(document.Data.Waypoints.Select(w => (w.Name, w.Elevation, w.Time)), reopened.Data.Waypoints.Select(w => (w.Name, w.Elevation, w.Time)));
        Assert.Empty(store.CheckSave(document));
    }

    private sealed class ShapeComparer : IEqualityComparer<TrailShape>
    {
        public static ShapeComparer Instance { get; } = new();

        public bool Equals(TrailShape? x, TrailShape? y) =>
            x is not null && y is not null
            && x.Waypoints.SequenceEqual(y.Waypoints)
            && x.Routes.Count == y.Routes.Count && x.Routes.Zip(y.Routes).All(p => p.First.SequenceEqual(p.Second))
            && x.Tracks.Count == y.Tracks.Count
            && x.Tracks.Zip(y.Tracks).All(t => t.First.Count == t.Second.Count && t.First.Zip(t.Second).All(s => s.First.SequenceEqual(s.Second)));

        public int GetHashCode(TrailShape obj) => 0;
    }
}
