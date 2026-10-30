using System.IO;
using Polaris.Gps.Domain.Documents;
using Polaris.Gps.Gpx;

namespace Polaris.Gps.App.Services;

/// <summary>
/// Loads GPX, KML, KMZ, TCX, OziExplorer and CompeGPS/TwoNav files off the UI thread (NFR-01, CONV-01). The others become new unsaved GPX
/// documents named after the source (CONV-02); their file is never written. Saving writes GPX, verified and without
/// risk of truncating the file (increment 4).
/// </summary>
public sealed class FileDocumentStore : IDocumentStore
{
    public Task<TrailDocument> LoadAsync(
        string filePath, DocumentFormat format, IProgress<double>? progress, CancellationToken cancellationToken) =>
        Task.Run(() => Read(filePath, format, progress, cancellationToken), cancellationToken);

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
        await Task.Run(() => GpxConversion.Write(bytes, shape, filePath), cancellationToken);
    }

    private static TrailDocument Read(string filePath, DocumentFormat format, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (format != DocumentFormat.Gpx)
        {
            throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }

        var (source, result) = TrailFileReader.ReadFile(filePath, progress, cancellationToken);
        return source == SourceFormat.Gpx
            ? new TrailDocument(filePath, format, result.Data, result.Warnings)
            : TrailDocument.CreateImported(format, Path.ChangeExtension(Path.GetFileName(filePath), ".gpx"), result.Data, result.Warnings, filePath);
    }
}
