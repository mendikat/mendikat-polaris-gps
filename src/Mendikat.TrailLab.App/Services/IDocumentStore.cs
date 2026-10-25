using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Gpx;

namespace Mendikat.TrailLab.App.Services;

/// <summary>Loads and saves documents. File I/O runs off the UI thread (NFR-01).</summary>
public interface IDocumentStore
{
    /// <exception cref="DocumentLoadException">The file cannot be loaded for an expected reason.</exception>
    Task<TrailDocument> LoadAsync(string filePath, DocumentFormat format, CancellationToken cancellationToken);

    /// <summary>Whether saving is implemented for the format.</summary>
    bool CanSave(DocumentFormat format);

    /// <summary>Differences between the document and the file it would produce, to confirm before saving (GPX-04, WPE-04).</summary>
    IReadOnlyList<SaveIssue> CheckSave(TrailDocument document);

    /// <summary>Writes the document in its own format, safely (SAVE-01, SAVE-03).</summary>
    /// <exception cref="IOException">The file could not be written; the destination is unchanged.</exception>
    /// <exception cref="UnauthorizedAccessException">No permission to write the destination.</exception>
    Task SaveAsync(TrailDocument document, string filePath, CancellationToken cancellationToken);
}
