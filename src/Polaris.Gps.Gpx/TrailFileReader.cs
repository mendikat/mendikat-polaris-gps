using Polaris.Gps.Domain.Documents;

namespace Polaris.Gps.Gpx;

/// <summary>A file read in any supported format; <see cref="TrailFileResult.Data"/> is always GPX data.</summary>
public sealed record SourceFileResult(SourceFormat Format, TrailFileResult Result);

/// <summary>
/// Opens GPX, KML, KMZ, TCX, OziExplorer (.plt, .wpt) and CompeGPS/TwoNav (.trk, .wpt) (CONV-01). The format is
/// detected from the content (ZIP signature, first text line or XML root), so a
/// renamed file is still recognised; the extension only decides which reader reports the error of a file that is
/// neither (empty, not well-formed).
/// </summary>
public static class TrailFileReader
{
    /// <summary>Extensions of the formats that can be opened.</summary>
    public static IReadOnlyList<string> Extensions { get; } = [".gpx", ".kml", ".kmz", ".tcx", ".plt", ".wpt", ".trk"];

    /// <summary>Reads a file from disk. Call it off the UI thread (NFR-01).</summary>
    /// <exception cref="DocumentLoadException">Missing, empty, unreadable, unsupported or invalid file.</exception>
    /// <exception cref="OperationCanceledException">Cancelled.</exception>
    public static SourceFileResult ReadFile(string filePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var info = new FileInfo(filePath);
        if (!info.Exists)
        {
            throw new DocumentLoadException(DocumentLoadError.FileNotFound);
        }

        if (info.Length == 0)
        {
            throw new DocumentLoadException(DocumentLoadError.EmptyFile);
        }

        try
        {
            using var stream = new FileStream(
                filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 64 * 1024, FileOptions.SequentialScan);
            return Read(stream, filePath, progress, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new DocumentLoadException(DocumentLoadError.AccessDenied, innerException: ex);
        }
        catch (IOException ex)
        {
            throw new DocumentLoadException(DocumentLoadError.ReadFailed, innerException: ex);
        }
    }

    /// <exception cref="DocumentLoadException">Unsupported or invalid file.</exception>
    /// <exception cref="OperationCanceledException">Cancelled.</exception>
    public static SourceFileResult Read(Stream stream, string fileName, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var format = Detect(stream, fileName);
        var result = format switch
        {
            SourceFormat.Gpx => new GpxReader().Read(stream, progress, cancellationToken),
            SourceFormat.Kml => new KmlReader().Read(stream, progress, cancellationToken),
            SourceFormat.Kmz => new KmzReader().Read(stream, progress, cancellationToken),
            SourceFormat.Tcx => new TcxReader().Read(stream, progress, cancellationToken),
            SourceFormat.OziExplorer => new OziExplorerReader().Read(stream, progress, cancellationToken),
            SourceFormat.CompeGps => new CompeGpsReader().Read(stream, progress, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(fileName), format, null),
        };
        return new SourceFileResult(format, result);
    }

    /// <summary>Format of a seekable stream; the position is restored.</summary>
    /// <exception cref="DocumentLoadException">Well-formed XML of another kind.</exception>
    public static SourceFormat Detect(Stream stream, string fileName)
    {
        if (!stream.CanSeek)
        {
            throw new ArgumentException("Seekable stream required.", nameof(stream));
        }

        var start = stream.Position;
        try
        {
            Span<byte> signature = stackalloc byte[4];
            var read = stream.ReadAtLeast(signature, 4, throwOnEndOfStream: false);
            if (read == 4 && signature[0] == (byte)'P' && signature[1] == (byte)'K' && signature[2] is 3 or 5 && signature[3] is 4 or 6)
            {
                return SourceFormat.Kmz;
            }

            stream.Position = start;
            var head = LegacyText.Head(stream);
            if (OziExplorerReader.IsOziExplorer(head))
            {
                return SourceFormat.OziExplorer;
            }

            if (CompeGpsReader.IsCompeGps(head))
            {
                return SourceFormat.CompeGps;
            }

            return XmlInput.RootName(stream) switch
            {
                "gpx" => SourceFormat.Gpx,
                "kml" => SourceFormat.Kml,
                "TrainingCenterDatabase" => SourceFormat.Tcx,
                null => ByExtension(fileName),
                var other => throw new DocumentLoadException(DocumentLoadError.UnsupportedFormat, other),
            };
        }
        finally
        {
            stream.Position = start;
        }
    }

    private static SourceFormat ByExtension(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".kml" => SourceFormat.Kml,
        ".kmz" => SourceFormat.Kmz,
        ".tcx" => SourceFormat.Tcx,
        ".plt" or ".wpt" => SourceFormat.OziExplorer,
        ".trk" => SourceFormat.CompeGps,
        _ => SourceFormat.Gpx,
    };
}
