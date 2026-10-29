using System.Xml.Linq;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Model;

namespace Mendikat.TrailLab.Gpx;

/// <summary>Builds the GPX document that holds data converted from another format (CONV-02).</summary>
internal static class ConvertedData
{
    private static readonly XNamespace Gpx = GpxReader.Gpx11Namespace;

    public static string Name(SourceFormat source) => source switch
    {
        SourceFormat.OziExplorer => "OziExplorer",
        SourceFormat.CompeGps => "CompeGPS/TwoNav",
        _ => source.ToString().ToUpperInvariant(),
    };

    /// <summary>Empty GPX 1.1 data with the source name and description as metadata.</summary>
    public static TrailData Create(SourceFormat source, string? name, string? description, params (string Prefix, string Uri)[] namespaces)
    {
        var state = new GpxFormatState { SourceVersion = "1.1", ConvertedFrom = Name(source) };
        foreach (var (prefix, uri) in namespaces)
        {
            state.NamespaceDeclarations.Add(new(prefix, uri));
        }

        if (name is not null || description is not null)
        {
            var metadata = new XElement(
                Gpx + "metadata",
                name is null ? null : new XElement(Gpx + "name", name),
                description is null ? null : new XElement(Gpx + "desc", description));
            state.Metadata = new XmlFragment("metadata", metadata.ToString(SaveOptions.DisableFormatting));
        }

        return new TrailData { FormatState = state };
    }

    /// <summary>Common warnings of a conversion: the source format, times without zone, no data.</summary>
    public static List<LoadWarning> Warnings(SourceFormat source, TrailData data, int timesWithoutZone, IEnumerable<LoadWarning> specific)
    {
        var warnings = new List<LoadWarning> { new(LoadWarningKind.ConvertedFrom, Detail: Name(source)) };
        warnings.AddRange(specific);
        if (timesWithoutZone > 0)
        {
            warnings.Add(new LoadWarning(LoadWarningKind.TimesWithoutZone, timesWithoutZone));
        }

        if (data.IsEmpty)
        {
            warnings.Add(new LoadWarning(LoadWarningKind.NoData));
        }

        return warnings;
    }
}
