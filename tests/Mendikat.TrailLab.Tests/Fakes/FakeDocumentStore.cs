using Mendikat.TrailLab.App.Services;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Domain.Model;
using Mendikat.TrailLab.Gpx;

namespace Mendikat.TrailLab.Tests.Fakes;

/// <summary>In-memory store: any path loads (with one waypoint) unless an error is registered for it.</summary>
internal sealed class FakeDocumentStore : IDocumentStore
{
    public Dictionary<string, DocumentLoadError> LoadErrors { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Content returned for specific paths; others get a single waypoint at (42, -1).</summary>
    public Dictionary<string, TrailData> Contents { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool SavingSupported { get; set; }

    public bool FailSaving { get; set; }

    public List<string> SavedPaths { get; } = [];

    public Task<TrailDocument> LoadAsync(string filePath, DocumentFormat format, CancellationToken cancellationToken)
    {
        if (LoadErrors.TryGetValue(filePath, out var error))
        {
            throw new DocumentLoadException(error, line: 3);
        }

        if (!Contents.TryGetValue(filePath, out var data))
        {
            data = new TrailData();
            data.Waypoints.Add(new TrailPoint(new GeoPosition(42, -1)));
        }

        return Task.FromResult(new TrailDocument(filePath, format, data));
    }

    public bool CanSave(DocumentFormat format) => SavingSupported;

    public List<SaveIssue> Issues { get; } = [];

    public IReadOnlyList<SaveIssue> CheckSave(TrailDocument document) => Issues;

    public Exception? SaveFailure { get; set; }

    public Task SaveAsync(TrailDocument document, string filePath, CancellationToken cancellationToken)
    {
        if (FailSaving)
        {
            throw SaveFailure ?? new IOException("Simulated write failure");
        }

        SavedPaths.Add(filePath);
        return Task.CompletedTask;
    }
}
