namespace Polaris.Gps.Domain.Model;

/// <summary>
/// An XML element kept verbatim because the model has no typed property for it (unknown fields, extensions).
/// <paramref name="LocalName"/> lets the writer place it in schema order; <paramref name="Xml"/> is the
/// serialized element including the namespace declarations it needs.
/// </summary>
public sealed record XmlFragment(string LocalName, string Xml);
