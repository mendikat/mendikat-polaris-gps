using Mendikat.TrailLab.Domain.Documents;

namespace Mendikat.TrailLab.Tests.Domain;

public sealed class TrailDocumentTests
{
    [Fact]
    [Trait("Req", "SAVE-04")]
    public void New_document_has_no_unsaved_changes()
    {
        var document = new TrailDocument(@"C:\data\route.gpx", DocumentFormat.Gpx);

        Assert.False(document.IsDirty);
        Assert.Equal("route.gpx", document.FileName);
    }

    [Fact]
    [Trait("Req", "SAVE-04")]
    public void Change_marks_dirty_and_save_clears_it()
    {
        var document = new TrailDocument(@"C:\data\route.gpx", DocumentFormat.Gpx);
        var events = 0;
        document.Changed += (_, _) => events++;

        document.MarkChanged();
        Assert.True(document.IsDirty);

        document.MarkSaved(@"C:\data\copy.gpx");
        Assert.False(document.IsDirty);
        Assert.Equal(@"C:\data\copy.gpx", document.FilePath);
        Assert.Equal(2, events);
    }

    [Theory]
    [Trait("Req", "APP-07")]
    [InlineData(@"C:\data\route.gpx", true)]
    [InlineData(@"c:\DATA\Route.GPX", true)]
    [InlineData(@"C:\data\..\data\route.gpx", true)]
    [InlineData(@"C:\data\other.gpx", false)]
    public void IsSameFile_compares_full_paths_case_insensitively(string path, bool expected)
    {
        var document = new TrailDocument(@"C:\data\route.gpx", DocumentFormat.Gpx);

        Assert.Equal(expected, document.IsSameFile(path));
    }
}
