namespace Mendikat.TrailLab.Gpx;

internal static class TextProbe
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// True if the stream holds nothing but an optional UTF-8 BOM and ASCII whitespace.
    /// Reads at most 64 KiB and restores the position (the stream must be seekable).
    /// </summary>
    public static bool IsBlank(Stream stream)
    {
        if (!stream.CanSeek)
        {
            throw new ArgumentException("Seekable stream required.", nameof(stream));
        }

        var start = stream.Position;
        var buffer = new byte[64 * 1024];
        var read = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
        stream.Position = start;

        var span = buffer.AsSpan(0, read);
        if (span.StartsWith(Utf8Bom))
        {
            span = span[Utf8Bom.Length..];
        }

        foreach (var b in span)
        {
            if (b is not ((byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'))
            {
                return false;
            }
        }

        // Only blank if the whole stream fitted in the buffer.
        return read < buffer.Length;
    }
}
