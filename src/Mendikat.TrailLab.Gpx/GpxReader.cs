using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Domain.Model;

namespace Mendikat.TrailLab.Gpx;

/// <summary>
/// Reads GPX 1.0 and 1.1 (GPX-01). Typed fields go to the model; everything else is kept as verbatim XML
/// so it can be written back (GPX-03, ADR-0005). Invalid content raises <see cref="DocumentLoadException"/>.
/// </summary>
public sealed partial class GpxReader
{
    public const string Gpx11Namespace = "http://www.topografix.com/GPX/1/1";
    public const string Gpx10Namespace = "http://www.topografix.com/GPX/1/0";

    private int _timesWithoutZone;

    public TrailFileResult Read(Stream stream, CancellationToken cancellationToken = default)
    {
        _timesWithoutZone = 0;
        var root = LoadRoot(stream);
        var ns = root.Name.Namespace;
        var warnings = new List<LoadWarning>();
        var version = DetectVersion(root, warnings);

        var state = new GpxFormatState { SourceVersion = version, Creator = (string?)root.Attribute("creator") };
        ReadRootAttributes(root, state);

        var data = new TrailData { FormatState = state };
        foreach (var element in root.Elements())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element.Name.Namespace != ns)
            {
                state.OtherElements.Add(Fragment(element));
                continue;
            }

            switch (element.Name.LocalName)
            {
                case "wpt":
                    data.Waypoints.Add(ReadPoint(element));
                    break;
                case "rte":
                    data.Routes.Add(ReadRoute(element, ns));
                    break;
                case "trk":
                    data.Tracks.Add(ReadTrack(element, ns, cancellationToken));
                    break;
                case "metadata":
                    state.Metadata = Fragment(element);
                    break;
                default:
                    state.OtherElements.Add(Fragment(element));
                    break;
            }
        }

        if (_timesWithoutZone > 0)
        {
            warnings.Add(new LoadWarning(LoadWarningKind.TimesWithoutZone, _timesWithoutZone));
        }

        if (data.IsEmpty)
        {
            warnings.Add(new LoadWarning(LoadWarningKind.NoData));
        }

        return new TrailFileResult(data, warnings);
    }

    private static XElement LoadRoot(Stream stream)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
        };
        if (TextProbe.IsBlank(stream))
        {
            throw new DocumentLoadException(DocumentLoadError.EmptyFile);
        }

        try
        {
            using var reader = XmlReader.Create(stream, settings);
            var document = XDocument.Load(reader, LoadOptions.SetLineInfo);
            var root = document.Root ?? throw new DocumentLoadException(DocumentLoadError.EmptyFile);
            if (root.Name.LocalName != "gpx")
            {
                throw new DocumentLoadException(DocumentLoadError.NotGpx, root.Name.LocalName);
            }

            return root;
        }
        catch (XmlException ex)
        {
            throw new DocumentLoadException(DocumentLoadError.InvalidXml, line: ex.LineNumber, innerException: ex);
        }
    }

    private static string DetectVersion(XElement root, List<LoadWarning> warnings)
    {
        var ns = root.Name.NamespaceName;
        if (ns == Gpx11Namespace)
        {
            return "1.1";
        }

        if (ns == Gpx10Namespace)
        {
            warnings.Add(new LoadWarning(LoadWarningKind.Gpx10));
            return "1.0";
        }

        if (ns.Length == 0)
        {
            warnings.Add(new LoadWarning(LoadWarningKind.GpxWithoutNamespace));
            return (string?)root.Attribute("version") == "1.0" ? "1.0" : "1.1";
        }

        throw new DocumentLoadException(DocumentLoadError.NotGpx, ns, LineOf(root));
    }

    private static void ReadRootAttributes(XElement root, GpxFormatState state)
    {
        foreach (var attribute in root.Attributes())
        {
            if (attribute.IsNamespaceDeclaration)
            {
                if (attribute.Value != Gpx11Namespace && attribute.Value != Gpx10Namespace)
                {
                    var prefix = attribute.Name.Namespace == XNamespace.None ? string.Empty : attribute.Name.LocalName;
                    state.NamespaceDeclarations.Add(new(prefix, attribute.Value));
                }
            }
            else if (attribute.Name.Namespace != XNamespace.None || attribute.Name.LocalName is not ("version" or "creator"))
            {
                state.OtherAttributes.Add(new(attribute.Name.NamespaceName, attribute.Name.LocalName, attribute.Value));
            }
        }
    }

    private Track ReadTrack(XElement element, XNamespace ns, CancellationToken cancellationToken)
    {
        var track = new Track();
        foreach (var child in element.Elements())
        {
            if (child.Name == ns + "trkseg")
            {
                cancellationToken.ThrowIfCancellationRequested();
                track.Segments.Add(ReadSegment(child, ns));
            }
            else if (!TryReadDescriptive(child, ns, out var field))
            {
                track.OtherElements.Add(Fragment(child));
            }
            else
            {
                AssignDescriptive(field, child.Value, v => track.Name = v, v => track.Comment = v, v => track.Description = v, v => track.Type = v);
            }
        }

        return track;
    }

    private TrackSegment ReadSegment(XElement element, XNamespace ns)
    {
        var segment = new TrackSegment();
        foreach (var child in element.Elements())
        {
            if (child.Name == ns + "trkpt")
            {
                segment.Points.Add(ReadPoint(child));
            }
            else
            {
                segment.OtherElements.Add(Fragment(child));
            }
        }

        return segment;
    }

    private Route ReadRoute(XElement element, XNamespace ns)
    {
        var route = new Route();
        foreach (var child in element.Elements())
        {
            if (child.Name == ns + "rtept")
            {
                route.Points.Add(ReadPoint(child));
            }
            else if (!TryReadDescriptive(child, ns, out var field))
            {
                route.OtherElements.Add(Fragment(child));
            }
            else
            {
                AssignDescriptive(field, child.Value, v => route.Name = v, v => route.Comment = v, v => route.Description = v, v => route.Type = v);
            }
        }

        return route;
    }

    private TrailPoint ReadPoint(XElement element)
    {
        var ns = element.Name.Namespace;
        var point = new TrailPoint(ReadPosition(element));
        foreach (var child in element.Elements())
        {
            if (child.Name.Namespace != ns)
            {
                point.OtherElements.Add(Fragment(child));
                continue;
            }

            switch (child.Name.LocalName)
            {
                case "ele":
                    point.Elevation = ParseOptionalDouble(child);
                    break;
                case "time":
                    point.Time = ParseOptionalTime(child);
                    break;
                case "name":
                    point.Name = child.Value;
                    break;
                case "cmt":
                    point.Comment = child.Value;
                    break;
                case "desc":
                    point.Description = child.Value;
                    break;
                case "sym":
                    point.Symbol = child.Value;
                    break;
                case "type":
                    point.Type = child.Value;
                    break;
                default:
                    point.OtherElements.Add(Fragment(child));
                    break;
            }
        }

        return point;
    }

    private static GeoPosition ReadPosition(XElement element)
    {
        var latText = (string?)element.Attribute("lat");
        var lonText = (string?)element.Attribute("lon");
        if (!TryParseDouble(latText, out var lat) || !GeoPosition.IsValidLatitude(lat))
        {
            throw new DocumentLoadException(DocumentLoadError.InvalidCoordinate, $"lat=\"{latText}\"", LineOf(element));
        }

        if (!TryParseDouble(lonText, out var lon) || !GeoPosition.IsValidLongitude(lon))
        {
            throw new DocumentLoadException(DocumentLoadError.InvalidCoordinate, $"lon=\"{lonText}\"", LineOf(element));
        }

        return new GeoPosition(lat, lon);
    }

    private static double? ParseOptionalDouble(XElement element)
    {
        var text = element.Value.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        return TryParseDouble(text, out var value)
            ? value
            : throw new DocumentLoadException(DocumentLoadError.InvalidNumber, $"{element.Name.LocalName}=\"{text}\"", LineOf(element));
    }

    private DateTimeOffset? ParseOptionalTime(XElement element)
    {
        var text = element.Value.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        var hasZone = ZoneSuffix().IsMatch(text);
        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value))
        {
            throw new DocumentLoadException(DocumentLoadError.InvalidDate, $"time=\"{text}\"", LineOf(element));
        }

        if (!hasZone)
        {
            _timesWithoutZone++;
        }

        return value.ToUniversalTime();
    }

    private static bool TryParseDouble(string? text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    private static bool TryReadDescriptive(XElement child, XNamespace ns, out string field)
    {
        field = child.Name.LocalName;
        return child.Name.Namespace == ns && field is "name" or "cmt" or "desc" or "type";
    }

    private static void AssignDescriptive(
        string field, string value, Action<string> name, Action<string> comment, Action<string> description, Action<string> type)
    {
        switch (field)
        {
            case "name": name(value); break;
            case "cmt": comment(value); break;
            case "desc": description(value); break;
            default: type(value); break;
        }
    }

    /// <summary>Serializes an element verbatim; LINQ to XML adds the namespace declarations it needs.</summary>
    private static XmlFragment Fragment(XElement element) =>
        new(element.Name.LocalName, element.ToString(SaveOptions.DisableFormatting));

    private static int? LineOf(XObject node) =>
        node is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : null;

    /// <summary>"Z" or "±hh:mm" / "±hhmm" at the end of an xsd:dateTime.</summary>
    [GeneratedRegex(@"(Z|[+-]\d{2}:?\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex ZoneSuffix();
}
