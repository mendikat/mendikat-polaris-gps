using System.Globalization;
using System.Text;
using Polaris.Gps.Domain.Documents;
using Polaris.Gps.Domain.Geo;

namespace Polaris.Gps.Gpx;

/// <summary>Datum of an OziExplorer or CompeGPS/TwoNav text file. Only these two can be converted to WGS84.</summary>
internal enum LegacyDatum
{
    Wgs84,
    Ed50,
}

/// <summary>
/// Helpers shared by the OziExplorer and CompeGPS/TwoNav readers: text files of one record per line, written by
/// Windows programs in UTF-8 (with or without BOM) or in the ANSI code page (Windows-1252).
/// </summary>
internal static class LegacyText
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Whole text of the stream: UTF-8 when it is valid UTF-8, otherwise Windows-1252.</summary>
    public static string ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return Decode(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    }

    /// <summary>First characters of a seekable stream, for format detection; the position is restored.</summary>
    public static string Head(Stream stream, int bytes = 512)
    {
        var start = stream.Position;
        var buffer = new byte[bytes];
        var read = stream.ReadAtLeast(buffer, bytes, throwOnEndOfStream: false);
        stream.Position = start;
        return Encoding.Latin1.GetString(buffer, 0, read).TrimStart('﻿', 'ï', '»', '¿');
    }

    /// <summary>Lines of a text, without line terminators.</summary>
    public static string[] Lines(string text) => text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);

    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            bytes = bytes[3..];
        }

        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Windows1252.GetString(bytes);
        }
    }

    private static Encoding Windows1252
    {
        get
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252);
        }
    }

    /// <summary>Datum line as mendikat.net reads it: "WGS 84" or "European…" (ED50); ETRS89 is taken as WGS84.</summary>
    /// <exception cref="DocumentLoadException">Any other datum.</exception>
    public static LegacyDatum ParseDatum(string text, int line)
    {
        if (text.Contains("WGS 84", StringComparison.OrdinalIgnoreCase)
            || text.Contains("WGS84", StringComparison.OrdinalIgnoreCase)
            || text.Contains("ETRS", StringComparison.OrdinalIgnoreCase))
        {
            return LegacyDatum.Wgs84;
        }

        if (text.Contains("European", StringComparison.OrdinalIgnoreCase))
        {
            return LegacyDatum.Ed50;
        }

        throw new DocumentLoadException(DocumentLoadError.UnsupportedDatum, text.Trim(), line);
    }

    /// <summary>The WGS84 position of coordinates read in <paramref name="datum"/>.</summary>
    /// <exception cref="DocumentLoadException">Out of range, or ED50 outside Spain and Portugal.</exception>
    public static GeoPosition Position(double latitude, double longitude, LegacyDatum datum, int line)
    {
        if (!GeoPosition.IsValidLatitude(latitude) || !GeoPosition.IsValidLongitude(longitude))
        {
            throw new DocumentLoadException(DocumentLoadError.InvalidCoordinate,
                string.Create(CultureInfo.InvariantCulture, $"{latitude}, {longitude}"), line);
        }

        if (datum == LegacyDatum.Ed50)
        {
            var zone = Ed50.ZoneOf(longitude);
            if (!Ed50.IsSupportedZone(zone))
            {
                throw new DocumentLoadException(DocumentLoadError.UnsupportedDatum, $"ED50, huso {zone}", line);
            }

            (latitude, longitude) = Ed50.ToWgs84(latitude, longitude, zone);
        }

        return new GeoPosition(latitude, longitude);
    }

    /// <exception cref="DocumentLoadException">Not a number.</exception>
    public static double Number(string? text, DocumentLoadError error, int line) =>
        XmlInput.TryParseDouble(text, out var value) ? value : throw new DocumentLoadException(error, text?.Trim(), line);

    /// <summary>Trimmed text, or null if empty.</summary>
    public static string? Text(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
