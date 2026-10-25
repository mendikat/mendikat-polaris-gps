using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Domain.Model;
using Mendikat.TrailLab.Gpx;

namespace Mendikat.TrailLab.Tests.Gpx;

public sealed class GpxWriterTests
{
    private static readonly XNamespace Gpx = GpxReader.Gpx11Namespace;

    private static TrailData Read(string fixture)
    {
        using var stream = Fixture.Open($"gpx/{fixture}");
        return new GpxReader().Read(stream).Data;
    }

    private static TrailData ReadBytes(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return new GpxReader().Read(stream).Data;
    }

    /// <summary>Validates against the official GPX 1.1 schema (GPX-04). Returns the validation errors.</summary>
    internal static List<string> SchemaErrors(byte[] bytes)
    {
        var schemas = new XmlSchemaSet();
        schemas.Add(GpxReader.Gpx11Namespace, Fixture.PathOf("schema/gpx-1.1.xsd"));
        var errors = new List<string>();
        var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema, Schemas = schemas };
        settings.ValidationEventHandler += (_, e) => errors.Add($"{e.Severity}: {e.Message}");
        using var reader = XmlReader.Create(new MemoryStream(bytes), settings);
        while (reader.Read())
        {
        }

        return errors.Where(e => e.StartsWith("Error", StringComparison.Ordinal)).ToList();
    }

    /// <summary>Compares fragments ignoring namespace declaration attributes (prefix bookkeeping, not data).</summary>
    private static bool SameXml(XmlFragment a, XmlFragment b)
    {
        static XElement Clean(XmlFragment f)
        {
            var e = XElement.Parse(f.Xml);
            e.DescendantsAndSelf().Attributes().Where(x => x.IsNamespaceDeclaration).Remove();
            return e;
        }

        return XNode.DeepEquals(Clean(a), Clean(b));
    }

    private static void AssertSamePoint(TrailPoint expected, TrailPoint actual)
    {
        Assert.Equal(expected.Position, actual.Position);
        Assert.Equal(expected.Elevation, actual.Elevation);
        Assert.Equal(expected.Time, actual.Time);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Comment, actual.Comment);
        Assert.Equal(expected.Description, actual.Description);
        Assert.Equal(expected.Symbol, actual.Symbol);
        Assert.Equal(expected.Type, actual.Type);
        Assert.Equal(expected.OtherElements.Count, actual.OtherElements.Count);
        Assert.All(expected.OtherElements.Zip(actual.OtherElements), p => Assert.True(SameXml(p.First, p.Second), p.First.Xml));
    }

    [Fact]
    [Trait("Req", "GPX-03")]
    [Trait("Acc", "ACC-GPX-01")]
    public void Round_trip_keeps_points_standard_fields_and_extensions()
    {
        var original = Read("valid-11.gpx");

        var copy = ReadBytes(new GpxWriter().Write(original));

        Assert.Equal(original.Waypoints.Count, copy.Waypoints.Count);
        original.Waypoints.Zip(copy.Waypoints).ToList().ForEach(p => AssertSamePoint(p.First, p.Second));
        var (route, routeCopy) = (original.Routes.Single(), copy.Routes.Single());
        Assert.Equal(route.Name, routeCopy.Name);
        route.Points.Zip(routeCopy.Points).ToList().ForEach(p => AssertSamePoint(p.First, p.Second));
        var (track, trackCopy) = (original.Tracks.Single(), copy.Tracks.Single());
        Assert.Equal((track.Name, track.Type), (trackCopy.Name, trackCopy.Type));
        Assert.Equal(track.Segments.Count, trackCopy.Segments.Count);
        track.AllPoints.Zip(trackCopy.AllPoints).ToList().ForEach(p => AssertSamePoint(p.First, p.Second));
    }

    [Fact]
    [Trait("Req", "GPX-03")]
    public void Round_trip_keeps_metadata_root_attributes_and_root_extensions()
    {
        var original = Read("valid-11.gpx");
        var state = (GpxFormatState)original.FormatState!;

        var bytes = new GpxWriter().Write(original);
        var copy = (GpxFormatState)ReadBytes(bytes).FormatState!;

        Assert.Equal(state.Creator, copy.Creator);
        Assert.True(SameXml(state.Metadata!, copy.Metadata!));
        Assert.Equal(state.OtherAttributes, copy.OtherAttributes);
        Assert.Equal(state.NamespaceDeclarations, copy.NamespaceDeclarations);
        Assert.True(SameXml(state.OtherElements.Single(), copy.OtherElements.Single()));
        Assert.Contains("<gpxtpx:hr>110</gpxtpx:hr>", Encoding.UTF8.GetString(bytes)); // prefix kept
        Assert.Contains("<custom:info xmlns:custom=\"urn:example:custom\">", Encoding.UTF8.GetString(bytes));
    }

    [Theory]
    [Trait("Req", "GPX-04")]
    [InlineData("valid-11.gpx")]
    [InlineData("missing-data.gpx")]
    [InlineData("no-points.gpx")]
    [InlineData("gpx10.gpx")]
    [InlineData("no-namespace.gpx")]
    public void Output_is_valid_gpx_11(string fixture)
    {
        var bytes = new GpxWriter().Write(Read(fixture));

        Assert.Empty(SchemaErrors(bytes));
        Assert.Equal("1.1", (string?)XDocument.Load(new MemoryStream(bytes)).Root!.Attribute("version"));
    }

    [Fact]
    [Trait("Req", "GPX-04")]
    public void Gpx_10_is_upgraded_keeping_fields_without_equivalent_in_extensions()
    {
        var original = Read("gpx10.gpx");
        Assert.Equal(SaveIssueKind.GpxVersionUpgrade, Assert.Single(GpxWriter.Check(original)).Kind);

        var document = XDocument.Load(new MemoryStream(new GpxWriter().Write(original)));

        var root = document.Root!;
        Assert.Equal(Gpx, root.Name.Namespace);
        Assert.Equal("2005-06-01T10:00:00Z", (string?)root.Element(Gpx + "metadata")?.Element(Gpx + "time"));
        var point = root.Descendants(Gpx + "trkpt").First();
        Assert.Equal("10", (string?)point.Element(Gpx + "ele"));
        var extensions = point.Element(Gpx + "extensions")!.Elements().Select(e => e.Name).ToList();
        XNamespace gpx10 = GpxReader.Gpx10Namespace;
        Assert.Equal([gpx10 + "course", gpx10 + "speed"], extensions);
    }

    [Fact]
    [Trait("Req", "GPX-05")]
    [Trait("Acc", "ACC-GPX-02")]
    public void Missing_values_are_not_invented()
    {
        var text = Encoding.UTF8.GetString(new GpxWriter().Write(Read("missing-data.gpx")));

        Assert.DoesNotContain("<ele>", text);
        Assert.Single(ReadBytes(Encoding.UTF8.GetBytes(text)).Tracks[0].AllPoints, p => p.Time is not null);
    }

    [Fact]
    [Trait("Req", "GPX-03")]
    public void Coordinates_elevations_and_times_are_written_without_rounding()
    {
        var data = new TrailData();
        data.Waypoints.Add(new TrailPoint(new GeoPosition(42.98530001234567, -2.0786000987654321))
        {
            Elevation = 1431.123456789,
            Time = new DateTimeOffset(2026, 5, 1, 7, 0, 0, 123, TimeSpan.Zero),
        });

        var copy = ReadBytes(new GpxWriter().Write(data)).Waypoints.Single();

        AssertSamePoint(data.Waypoints[0], copy);
        Assert.Empty(SchemaErrors(new GpxWriter().Write(data)));
    }

    [Fact]
    [Trait("Req", "GPX-03")]
    public void Edits_are_written_and_untouched_points_keep_their_extensions()
    {
        var data = Read("valid-11.gpx");
        var segment = data.Tracks[0].Segments[0].Points;
        segment.RemoveAt(1);
        segment.Insert(1, new TrailPoint(new GeoPosition(42.99, -2.07)) { IsAdded = true });
        data.Waypoints[0].Name = "Txindoki (1.346 m)";

        var copy = ReadBytes(new GpxWriter().Write(data));

        Assert.Equal("Txindoki (1.346 m)", copy.Waypoints[0].Name);
        Assert.Equal(new GeoPosition(42.99, -2.07), copy.Tracks[0].Segments[0].Points[1].Position);
        Assert.Contains(copy.Tracks[0].Segments[0].Points[0].OtherElements, e => e.LocalName == "extensions");
    }
}
