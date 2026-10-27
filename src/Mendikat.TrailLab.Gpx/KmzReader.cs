using System.IO.Compression;
using Mendikat.TrailLab.Domain.Documents;

namespace Mendikat.TrailLab.Gpx;

/// <summary>
/// Reads a KMZ: a ZIP whose main document is doc.kml (as mendikat.net) or else the first .kml file (CONV-04).
/// Images and icons are ignored. Sizes are checked before and while extracting, and nothing is written to disk.
/// </summary>
public sealed class KmzReader
{
    /// <summary>Largest uncompressed KML accepted.</summary>
    public const long MaxKmlBytes = 256L * 1024 * 1024;

    /// <summary>More entries than this is not a normal KMZ.</summary>
    public const int MaxEntries = 10_000;

    /// <summary>Uncompressed/compressed ratio above which a large entry is treated as a ZIP bomb.</summary>
    public const int MaxCompressionRatio = 200;

    /// <summary>Entries smaller than this are never treated as a ZIP bomb (tiny files compress very well).</summary>
    private const long RatioCheckFrom = 1024 * 1024;

    /// <exception cref="DocumentLoadException">Not a valid KMZ, or its KML is invalid.</exception>
    /// <exception cref="OperationCanceledException">Cancelled.</exception>
    public TrailFileResult Read(Stream stream, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        using var kml = Extract(stream, cancellationToken);
        return new KmlReader().Read(kml, SourceFormat.Kmz, progress, cancellationToken);
    }

    private static MemoryStream Extract(Stream stream, CancellationToken cancellationToken)
    {
        ZipArchive archive;
        try
        {
            archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException ex)
        {
            throw new DocumentLoadException(DocumentLoadError.InvalidArchive, "zip", innerException: ex);
        }

        using (archive)
        {
            if (archive.Entries.Count > MaxEntries)
            {
                throw new DocumentLoadException(DocumentLoadError.InvalidArchive, $"{archive.Entries.Count} entries");
            }

            var entry = MainDocument(archive) ?? throw new DocumentLoadException(DocumentLoadError.InvalidArchive, "no .kml");
            if (entry.Length > MaxKmlBytes
                || (entry.Length > RatioCheckFrom && entry.Length > entry.CompressedLength * MaxCompressionRatio))
            {
                throw new DocumentLoadException(DocumentLoadError.InvalidArchive, $"{entry.FullName}: {entry.Length} bytes");
            }

            try
            {
                using var source = entry.Open();
                var copy = new MemoryStream();
                CopyLimited(source, copy, cancellationToken);
                copy.Position = 0;
                return copy;
            }
            catch (InvalidDataException ex)
            {
                throw new DocumentLoadException(DocumentLoadError.InvalidArchive, entry.FullName, innerException: ex);
            }
        }
    }

    /// <summary>doc.kml at the root, else the first .kml at the root, else the first .kml anywhere.</summary>
    private static ZipArchiveEntry? MainDocument(ZipArchive archive)
    {
        static bool IsKml(ZipArchiveEntry e) => e.FullName.EndsWith(".kml", StringComparison.OrdinalIgnoreCase);
        static bool AtRoot(ZipArchiveEntry e) => !e.FullName.Contains('/') && !e.FullName.Contains('\\');

        return archive.Entries.FirstOrDefault(e => string.Equals(e.FullName, "doc.kml", StringComparison.OrdinalIgnoreCase))
            ?? archive.Entries.FirstOrDefault(e => IsKml(e) && AtRoot(e))
            ?? archive.Entries.FirstOrDefault(IsKml);
    }

    /// <summary>The declared size can lie: stop as soon as more than the limit has been decompressed.</summary>
    private static void CopyLimited(Stream source, Stream target, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = source.Read(buffer)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            total += read;
            if (total > MaxKmlBytes)
            {
                throw new DocumentLoadException(DocumentLoadError.InvalidArchive, $"more than {MaxKmlBytes} bytes");
            }

            target.Write(buffer, 0, read);
        }
    }
}
