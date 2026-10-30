using Polaris.Gps.Domain.Documents;
using Polaris.Gps.Gpx;

namespace Polaris.Gps.App.Services;

/// <summary>Loads and saves documents. File I/O runs off the UI thread (NFR-01).</summary>
public interface IDocumentStore
{
    /// <param name="progress">Receives the fraction read, from 0 to 1, when the format can measure it (NFR-02).</param>
    /// <exception cref="DocumentLoadException">The file cannot be loaded for an expected reason.</exception>
    /// <exception cref="OperationCanceledException">Loading was cancelled.</exception>
    Task<TrailDocument> LoadAsync(string filePath, DocumentFormat format, IProgress<double>? progress, CancellationToken cancellationToken);

    /// <summary>Whether saving is implemented for the format.</summary>
    bool CanSave(DocumentFormat format);

    /// <summary>Differences between the document and the file it would produce, to confirm before saving (GPX-04, WPE-04).</summary>
    IReadOnlyList<SaveIssue> CheckSave(TrailDocument document);

    /// <summary>Writes the document in its own format, safely (SAVE-01, SAVE-03).</summary>
    /// <exception cref="IOException">The file could not be written; the destination is unchanged.</exception>
    /// <exception cref="UnauthorizedAccessException">No permission to write the destination.</exception>
    Task SaveAsync(TrailDocument document, string filePath, CancellationToken cancellationToken);
}
