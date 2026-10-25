using Mendikat.TrailLab.Domain.Model;

namespace Mendikat.TrailLab.Gpx;

/// <summary>Document-level GPX data kept to write the file back faithfully (ADR-0005).</summary>
public sealed class GpxFormatState
{
    /// <summary>"1.0" or "1.1" as read; files are always written as 1.1.</summary>
    public required string SourceVersion { get; init; }

    public string? Creator { get; init; }

    /// <summary>Namespace declarations of the root element (prefix → URI), excluding the GPX default namespace.</summary>
    public List<KeyValuePair<string, string>> NamespaceDeclarations { get; } = [];

    /// <summary>Root attributes other than version, creator and namespace declarations (e.g. xsi:schemaLocation).</summary>
    public List<XmlAttributeValue> OtherAttributes { get; } = [];

    /// <summary>The metadata element (1.1) kept verbatim; null if absent.</summary>
    public XmlFragment? Metadata { get; set; }

    /// <summary>Root-level elements other than wpt/rte/trk/metadata (extensions, GPX 1.0 header fields…).</summary>
    public List<XmlFragment> OtherElements { get; } = [];
}

/// <summary>An attribute identified by namespace URI and local name.</summary>
public sealed record XmlAttributeValue(string NamespaceUri, string LocalName, string Value);
