using System.IO;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Gpx;
using Mendikat.TrailLab.Gpx.Wpt;

namespace Mendikat.TrailLab.App.Services;

/// <summary>
/// Loads GPX and WPT files with the format readers, off the UI thread (NFR-01).
/// Saving writes the same format, verified and without risk of truncating the file (increment 4).
/// </summary>
public sealed class FileDocumentStore : IDocumentStore
{
    public async Task<TrailDocument> LoadAsync(
        string filePath, DocumentFormat format, IProgress<double>? progress, CancellationToken cancellationToken)
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
            return await Task.Run(() => Read(filePath, format, progress, cancellationToken), cancellationToken);
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

    public bool CanSave(DocumentFormat format) => true;

    public IReadOnlyList<SaveIssue> CheckSave(TrailDocument document) => TrailFileWriter.Check(document.Data, document.Format);

    /// <summary>
    /// Serializes on the calling (UI) thread, so the document cannot change meanwhile; verification and the
    /// safe write run in the background.
    /// </summary>
    public async Task SaveAsync(TrailDocument document, string filePath, CancellationToken cancellationToken)
    {
        var bytes = TrailFileWriter.Serialize(document.Data, document.Format);
        var shape = TrailFileWriter.Shape(document.Data);
        var format = document.Format;
        await Task.Run(
            () =>
            {
                if (!TrailFileWriter.Verify(bytes, format, shape))
                {
                    throw new SaveVerificationException("The generated file does not match the document.");
                }

                SafeFileWriter.Write(filePath, bytes);
            },
            cancellationToken);
    }

    /// <summary>WPT files are small (waypoints only) and are read at once; only GPX reports progress.</summary>
    private static TrailDocument Read(string filePath, DocumentFormat format, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 64 * 1024, FileOptions.SequentialScan);
        var result = format switch
        {
            DocumentFormat.Gpx => new GpxReader().Read(stream, progress, cancellationToken),
            DocumentFormat.Wpt => new WaypointFileReader().Read(stream),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };
        return new TrailDocument(filePath, format, result.Data, result.Warnings);
    }
}
