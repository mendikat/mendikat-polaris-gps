using System.Text;
using Mendikat.TrailLab.Domain.Documents;

namespace Mendikat.TrailLab.Gpx.Wpt;

/// <summary>Decodes a .wpt file, detects its dialect by content and delegates to it.</summary>
public sealed class WaypointFileReader
{
    private readonly IReadOnlyList<IWaypointFileFormat> _formats;

    static WaypointFileReader()
    {
        // Windows-1252 is not available in .NET by default.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public WaypointFileReader()
        : this([new OziExplorerFormat(), new CompeGpsFormat()])
    {
    }

    public WaypointFileReader(IReadOnlyList<IWaypointFileFormat> formats)
    {
        _formats = formats;
    }

    public static Encoding Windows1252 => Encoding.GetEncoding(1252);

    public TrailFileResult Read(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var (text, encoding) = Decode(buffer.ToArray());
        var lines = SplitLines(text);
        if (lines.All(string.IsNullOrWhiteSpace))
        {
            throw new DocumentLoadException(DocumentLoadError.EmptyFile);
        }

        var format = _formats.FirstOrDefault(f => f.CanRead(lines))
            ?? throw new DocumentLoadException(DocumentLoadError.UnsupportedWptDialect);
        return format.Read(lines, encoding);
    }

    /// <summary>
    /// UTF-8 if there is a BOM or the bytes are valid UTF-8 containing non-ASCII characters;
    /// Windows-1252 otherwise (ADR-0006). Pure ASCII is read as Windows-1252 (identical for ASCII).
    /// </summary>
    public static (string Text, Encoding Encoding) Decode(byte[] bytes)
    {
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        if (bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble))
        {
            return (utf8.GetString(bytes, 3, bytes.Length - 3), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }

        if (bytes.Any(b => b >= 0x80))
        {
            try
            {
                return (utf8.GetString(bytes), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (DecoderFallbackException)
            {
                // Not UTF-8: fall back to the ANSI code page used by OziExplorer and CompeGPS.
            }
        }

        return (Windows1252.GetString(bytes), Windows1252);
    }

    private static List<string> SplitLines(string text)
    {
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1); // trailing newline
        }

        return lines;
    }
}
