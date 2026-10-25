using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Mendikat.TrailLab.Domain.Model;

namespace Mendikat.TrailLab.Gpx;

/// <summary>
/// Writes GPX 1.1 (GPX-04, ADR-0005): typed fields plus the verbatim fragments kept by <see cref="GpxReader"/>,
/// in the element order of the GPX 1.1 schema. GPX 1.0 content is upgraded: fields with a 1.1 equivalent are
/// renamed or converted (url → link, root header → metadata) and the rest is kept inside "extensions".
/// </summary>
public sealed class GpxWriter
{
    public const string DefaultCreator = "Mendikat TrailLab";

    private static readonly XNamespace Ns11 = GpxReader.Gpx11Namespace;
    private static readonly XNamespace Ns10 = GpxReader.Gpx10Namespace;

    // Element order of the GPX 1.1 schema types.
    private static readonly string[] PointOrder =
        ["ele", "time", "magvar", "geoidheight", "name", "cmt", "desc", "src", "link", "sym", "type",
         "fix", "sat", "hdop", "vdop", "pdop", "ageofdgpsdata", "dgpsid", "extensions"];

    private static readonly string[] ItemOrder = ["name", "cmt", "desc", "src", "link", "number", "type", "extensions"];

    private static readonly string[] MetadataOrder = ["name", "desc", "author", "copyright", "link", "time", "keywords", "bounds", "extensions"];

    private static readonly string[] SegmentOrder = ["extensions"];

    /// <summary>(prefix, namespace) pairs declared on the root element of the file being written.</summary>
    private readonly HashSet<(string Prefix, string Uri)> _rootDeclarations = [];

    public static IReadOnlyList<SaveIssue> Check(TrailData data) =>
        data.FormatState is GpxFormatState { SourceVersion: "1.0" } ? [new SaveIssue(SaveIssueKind.GpxVersionUpgrade)] : [];

    public byte[] Write(TrailData data)
    {
        var state = data.FormatState as GpxFormatState;
        var upgrade = state?.SourceVersion == "1.0";
        _rootDeclarations.Clear();
        _rootDeclarations.Add((string.Empty, Ns11.NamespaceName));
        foreach (var (prefix, uri) in state?.NamespaceDeclarations.Where(d => d.Key.Length > 0) ?? [])
        {
            _rootDeclarations.Add((prefix, uri));
        }

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\r\n",
        };

        using var buffer = new MemoryStream();
        using (var writer = XmlWriter.Create(buffer, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("gpx", Ns11.NamespaceName);
            writer.WriteAttributeString("version", "1.1");
            writer.WriteAttributeString("creator", string.IsNullOrWhiteSpace(state?.Creator) ? DefaultCreator : state.Creator);
            WriteRootAttributes(writer, state, upgrade);

            var rootExtensions = new List<XElement>();
            WriteMetadata(writer, state, upgrade, rootExtensions);
            foreach (var waypoint in data.Waypoints)
            {
                WritePoint(writer, "wpt", waypoint, upgrade);
            }

            foreach (var route in data.Routes)
            {
                WriteRoute(writer, route, upgrade);
            }

            foreach (var track in data.Tracks)
            {
                WriteTrack(writer, track, upgrade);
            }

            var rootOthers = state?.OtherElements.Select(Parse).Where(e => !IsGpx10HeaderField(e, upgrade)) ?? [];
            WriteChildren(writer, [], rootOthers, ["extensions"], upgrade, rootExtensions);
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return buffer.ToArray();
    }

    private void WriteRootAttributes(XmlWriter writer, GpxFormatState? state, bool upgrade)
    {
        if (state is null)
        {
            return;
        }

        foreach (var (prefix, uri) in state.NamespaceDeclarations.Where(d => d.Key.Length > 0))
        {
            writer.WriteAttributeString("xmlns", prefix, null, uri);
        }

        foreach (var attribute in state.OtherAttributes)
        {
            var value = upgrade && attribute.LocalName == "schemaLocation"
                ? attribute.Value.Replace("GPX/1/0", "GPX/1/1", StringComparison.Ordinal)
                : attribute.Value;
            writer.WriteAttributeString(attribute.LocalName, attribute.NamespaceUri, value);
        }
    }

    /// <summary>GPX 1.1 metadata as read, or built from the GPX 1.0 root header fields.</summary>
    private void WriteMetadata(XmlWriter writer, GpxFormatState? state, bool upgrade, List<XElement> rootExtensions)
    {
        if (state is null)
        {
            return;
        }

        if (!upgrade)
        {
            if (state.Metadata is { } metadata)
            {
                WriteElement(writer, Parse(metadata));
            }

            return;
        }

        var header = state.OtherElements.Select(Parse).Where(e => IsGpx10HeaderField(e, upgrade: true)).ToList();
        if (header.Count == 0)
        {
            return;
        }

        var children = new List<XElement>();
        var author = new XElement(Ns11 + "author");
        foreach (var field in header)
        {
            switch (field.Name.LocalName)
            {
                case "author":
                    author.AddFirst(new XElement(Ns11 + "name", field.Value));
                    break;
                case "email":
                    var parts = field.Value.Split('@', 2);
                    if (parts.Length == 2)
                    {
                        author.Add(new XElement(Ns11 + "email", new XAttribute("id", parts[0]), new XAttribute("domain", parts[1])));
                    }
                    else
                    {
                        rootExtensions.Add(field);
                    }

                    break;
                default:
                    children.Add(field);
                    break;
            }
        }

        if (author.HasElements)
        {
            children.Add(author);
        }

        writer.WriteStartElement("metadata", Ns11.NamespaceName);
        WriteChildren(writer, [], children, MetadataOrder, upgrade: true, extraExtensions: []);
        writer.WriteEndElement();
    }

    private static bool IsGpx10HeaderField(XElement element, bool upgrade) =>
        upgrade && element.Name.Namespace == Ns10
        && element.Name.LocalName is "name" or "desc" or "author" or "email" or "url" or "urlname" or "time" or "keywords" or "bounds";

    private void WriteTrack(XmlWriter writer, Track track, bool upgrade)
    {
        writer.WriteStartElement("trk", Ns11.NamespaceName);
        WriteChildren(writer, Descriptive(track.Name, track.Comment, track.Description, track.Type), track.OtherElements.Select(Parse), ItemOrder, upgrade, []);
        foreach (var segment in track.Segments)
        {
            writer.WriteStartElement("trkseg", Ns11.NamespaceName);
            foreach (var point in segment.Points)
            {
                WritePoint(writer, "trkpt", point, upgrade);
            }

            WriteChildren(writer, [], segment.OtherElements.Select(Parse), SegmentOrder, upgrade, []);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private void WriteRoute(XmlWriter writer, Route route, bool upgrade)
    {
        writer.WriteStartElement("rte", Ns11.NamespaceName);
        WriteChildren(writer, Descriptive(route.Name, route.Comment, route.Description, route.Type), route.OtherElements.Select(Parse), ItemOrder, upgrade, []);
        foreach (var point in route.Points)
        {
            WritePoint(writer, "rtept", point, upgrade);
        }

        writer.WriteEndElement();
    }

    private void WritePoint(XmlWriter writer, string tag, TrailPoint point, bool upgrade)
    {
        writer.WriteStartElement(tag, Ns11.NamespaceName);
        writer.WriteAttributeString("lat", Number(point.Position.Latitude));
        writer.WriteAttributeString("lon", Number(point.Position.Longitude));

        var typed = new List<XElement>();
        if (point.Elevation is { } elevation)
        {
            typed.Add(new XElement(Ns11 + "ele", Number(elevation)));
        }

        if (point.Time is { } time)
        {
            typed.Add(new XElement(Ns11 + "time", XmlConvert.ToString(time.UtcDateTime, XmlDateTimeSerializationMode.Utc)));
        }

        typed.AddRange(Descriptive(point.Name, point.Comment, point.Description, null));
        if (point.Symbol is not null)
        {
            typed.Add(new XElement(Ns11 + "sym", point.Symbol));
        }

        if (point.Type is not null)
        {
            typed.Add(new XElement(Ns11 + "type", point.Type));
        }

        WriteChildren(writer, typed, point.OtherElements.Select(Parse), PointOrder, upgrade, []);
        writer.WriteEndElement();
    }

    private static List<XElement> Descriptive(string? name, string? comment, string? description, string? type)
    {
        var elements = new List<XElement>();
        Add("name", name);
        Add("cmt", comment);
        Add("desc", description);
        Add("type", type);
        return elements;

        void Add(string localName, string? value)
        {
            if (value is not null)
            {
                elements.Add(new XElement(Ns11 + localName, value));
            }
        }
    }

    /// <summary>
    /// Writes typed elements and kept fragments in schema order. Fragments in the GPX namespace are placed by name;
    /// "extensions" children (existing ones plus GPX 1.0 fields without a 1.1 equivalent) are merged into one element;
    /// elements of other namespaces found outside "extensions" are written last, as they were read.
    /// </summary>
    private void WriteChildren(
        XmlWriter writer, IEnumerable<XElement> typed, IEnumerable<XElement> others, string[] order, bool upgrade, List<XElement> extraExtensions)
    {
        var ordered = typed.ToList();
        var extensions = new List<XElement>();
        var foreign = new List<XElement>();

        foreach (var element in upgrade ? Upgrade(others.ToList(), order, extensions) : others)
        {
            if (element.Name == Ns11 + "extensions")
            {
                extensions.AddRange(element.Elements());
            }
            else if (element.Name.Namespace == Ns11 && Array.IndexOf(order, element.Name.LocalName) >= 0)
            {
                ordered.Add(element);
            }
            else
            {
                foreign.Add(element);
            }
        }

        extensions.AddRange(extraExtensions);
        foreach (var element in ordered.OrderBy(e => Array.IndexOf(order, e.Name.LocalName)))
        {
            WriteElement(writer, element);
        }

        if (extensions.Count > 0 && Array.IndexOf(order, "extensions") >= 0)
        {
            WriteElement(writer, new XElement(Ns11 + "extensions", extensions));
        }

        foreach (var element in foreign)
        {
            WriteElement(writer, element);
        }
    }

    /// <summary>
    /// GPX 1.0 → 1.1 for one parent's fragments: known fields move to the 1.1 namespace, url/urlname become a link,
    /// anything else (course, speed…) goes to <paramref name="extensions"/> keeping its 1.0 namespace.
    /// </summary>
    private static IEnumerable<XElement> Upgrade(List<XElement> elements, string[] order, List<XElement> extensions)
    {
        var url = elements.FirstOrDefault(e => e.Name == Ns10 + "url");
        var urlName = elements.FirstOrDefault(e => e.Name == Ns10 + "urlname");
        if (url is not null)
        {
            var link = new XElement(Ns11 + "link", new XAttribute("href", url.Value));
            if (urlName is not null)
            {
                link.Add(new XElement(Ns11 + "text", urlName.Value));
            }

            yield return link;
        }
        else if (urlName is not null)
        {
            extensions.Add(urlName);
        }

        foreach (var element in elements)
        {
            if (element.Name.Namespace != Ns10)
            {
                yield return element;
            }
            else if (element.Name.LocalName is "url" or "urlname")
            {
                continue;
            }
            else if (Array.IndexOf(order, element.Name.LocalName) >= 0 && element.Name.LocalName != "extensions")
            {
                yield return MoveToGpx11(element);
            }
            else
            {
                extensions.Add(element);
            }
        }
    }

    private static XElement MoveToGpx11(XElement element)
    {
        var copy = new XElement(element);
        foreach (var e in copy.DescendantsAndSelf().Where(e => e.Name.Namespace == Ns10))
        {
            e.Name = Ns11 + e.Name.LocalName;
            e.Attributes().Where(a => a.IsNamespaceDeclaration && a.Value == Ns10.NamespaceName).Remove();
        }

        return copy;
    }

    /// <summary>
    /// Writes an element, dropping only the namespace declarations already made on the root element (the GPX default
    /// namespace and the root prefixes); other declarations are kept so prefixes are preserved.
    /// </summary>
    private void WriteElement(XmlWriter writer, XElement element)
    {
        var redundant = element.DescendantsAndSelf().SelectMany(e => e.Attributes())
            .Where(a => a.IsNamespaceDeclaration && _rootDeclarations.Contains((DeclaredPrefix(a), a.Value)))
            .ToList();
        foreach (var declaration in redundant)
        {
            declaration.Remove();
        }

        element.WriteTo(writer);
    }

    private static string DeclaredPrefix(XAttribute declaration) =>
        declaration.Name.Namespace == XNamespace.None ? string.Empty : declaration.Name.LocalName;

    private static XElement Parse(XmlFragment fragment) => XElement.Parse(fragment.Xml);

    /// <summary>Shortest text that reads back to the same double (no rounding of the original values).</summary>
    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
