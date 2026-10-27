using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Mendikat.TrailLab.Domain.Documents;

namespace Mendikat.TrailLab.Gpx;

/// <summary>Safe XML loading and value parsing shared by the KML and TCX readers.</summary>
internal static partial class XmlInput
{
    /// <summary>No DTDs and no external resources: a file can never make the reader fetch or expand anything.</summary>
    public static XmlReaderSettings Settings => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
    };

    /// <summary>Loads the document with line numbers.</summary>
    /// <exception cref="DocumentLoadException">Blank or not well-formed.</exception>
    public static XElement LoadRoot(Stream stream)
    {
        if (stream.CanSeek && TextProbe.IsBlank(stream))
        {
            throw new DocumentLoadException(DocumentLoadError.EmptyFile);
        }

        try
        {
            using var reader = XmlReader.Create(stream, Settings);
            var document = XDocument.Load(reader, LoadOptions.SetLineInfo);
            return document.Root ?? throw new DocumentLoadException(DocumentLoadError.EmptyFile);
        }
        catch (XmlException ex)
        {
            throw new DocumentLoadException(DocumentLoadError.InvalidXml, line: ex.LineNumber, innerException: ex);
        }
    }

    /// <summary>Local name of the root element, reading only up to it (format detection).</summary>
    /// <returns>Null if the content is not XML.</returns>
    public static string? RootName(Stream stream)
    {
        try
        {
            using var reader = XmlReader.Create(stream, Settings);
            return reader.MoveToContent() == XmlNodeType.Element ? reader.LocalName : null;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    public static bool TryParseDouble(string? text, out double value) =>
        double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    /// <summary>Optional number; empty means absent.</summary>
    /// <exception cref="DocumentLoadException">Not a number.</exception>
    public static double? OptionalDouble(XElement? element)
    {
        var text = element?.Value.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return TryParseDouble(text, out var value)
            ? value
            : throw new DocumentLoadException(DocumentLoadError.InvalidNumber, $"{element!.Name.LocalName}=\"{Shorten(text)}\"", LineOf(element));
    }

    /// <summary>An xsd:dateTime as UTC. Times without zone are read as UTC and counted.</summary>
    /// <exception cref="DocumentLoadException">Not a date.</exception>
    public static DateTimeOffset? OptionalTime(XElement? element, ref int timesWithoutZone)
    {
        var text = element?.Value.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value))
        {
            throw new DocumentLoadException(DocumentLoadError.InvalidDate, $"{element!.Name.LocalName}=\"{Shorten(text)}\"", LineOf(element));
        }

        if (!ZoneSuffix().IsMatch(text))
        {
            timesWithoutZone++;
        }

        return value.ToUniversalTime();
    }

    public static int? LineOf(XObject node) =>
        node is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : null;

    /// <summary>Error details never carry long file content.</summary>
    public static string Shorten(string text) => text.Length <= 40 ? text : text[..40] + "…";

    /// <summary>Text of a child, trimmed; null when absent or blank.</summary>
    public static string? Text(XElement? element)
    {
        var text = element?.Value.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>"Z" or "±hh:mm" / "±hhmm" at the end of an xsd:dateTime.</summary>
    [GeneratedRegex(@"(Z|[+-]\d{2}:?\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex ZoneSuffix();
}
