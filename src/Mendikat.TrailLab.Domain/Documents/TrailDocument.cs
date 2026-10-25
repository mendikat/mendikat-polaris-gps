using Mendikat.TrailLab.Domain.Editing;
using Mendikat.TrailLab.Domain.Model;

namespace Mendikat.TrailLab.Domain.Documents;

/// <summary>
/// An in-memory document, loaded from a file or created empty with File › New (no file yet, APP-08).
/// It is independent from the file on disk: changes are never written back automatically (SAVE-05).
/// Edits go through <see cref="Execute"/> so they can be undone (EDIT-05).
/// </summary>
public sealed class TrailDocument
{
    private readonly Stack<HistoryEntry> _undo = new();
    private readonly Stack<HistoryEntry> _redo = new();

    // Each content state gets a unique id; the document is dirty when the current state is not the saved one,
    // so undoing back to the saved state clears the indicator (SAVE-04).
    private long _state;
    private long _savedState;
    private long _lastStateId;
    private string _fileName;

    public TrailDocument(string filePath, DocumentFormat format, TrailData? data = null, IReadOnlyList<LoadWarning>? warnings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = Path.GetFullPath(filePath);
        _fileName = Path.GetFileName(FilePath);
        Format = format;
        Data = data ?? new TrailData();
        Warnings = warnings ?? [];
    }

    private TrailDocument(DocumentFormat format, string title, TrailData data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        _fileName = title;
        Format = format;
        Data = data;
        Warnings = [];
    }

    /// <summary>Raised once per operation (EDIT-07): edit, undo, redo or save.</summary>
    public event EventHandler<DocumentChangedEventArgs>? Changed;

    /// <summary>
    /// Full path of the file the document is associated with (origin, or last "Save as" target);
    /// null for a new document that has never been saved.
    /// </summary>
    public string? FilePath { get; private set; }

    /// <summary>True for a new document that has no file yet: saving it asks where (APP-08).</summary>
    public bool IsNew => FilePath is null;

    public DocumentFormat Format { get; }

    public TrailData Data { get; }

    /// <summary>Non-fatal issues found when the file was loaded.</summary>
    public IReadOnlyList<LoadWarning> Warnings { get; }

    /// <summary>File name, or the provisional title of a new document (e.g. "Sin título 1.gpx").</summary>
    public string FileName => _fileName;

    /// <summary>True when the document differs from the last loaded or saved state (SAVE-04).</summary>
    public bool IsDirty => _state != _savedState;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>Creates an unsaved document with no file (File › New, APP-08). It starts without changes.</summary>
    public static TrailDocument CreateNew(DocumentFormat format, string title, TrailData data) => new(format, title, data);

    /// <summary>Applies a change and records it for undo. Clears the redo history.</summary>
    public void Execute(IEditCommand command)
    {
        command.Do();
        var before = _state;
        _state = ++_lastStateId;
        _undo.Push(new HistoryEntry(command, before, _state));
        _redo.Clear();
        Changed?.Invoke(this, DocumentChangedEventArgs.Content);
    }

    public bool Undo()
    {
        if (!_undo.TryPop(out var entry))
        {
            return false;
        }

        entry.Command.Undo();
        _state = entry.Before;
        _redo.Push(entry);
        Changed?.Invoke(this, DocumentChangedEventArgs.Content);
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPop(out var entry))
        {
            return false;
        }

        entry.Command.Do();
        _state = entry.After;
        _undo.Push(entry);
        Changed?.Invoke(this, DocumentChangedEventArgs.Content);
        return true;
    }

    /// <summary>Records a modification that cannot be undone (prefer <see cref="Execute"/>).</summary>
    public void MarkChanged()
    {
        _state = ++_lastStateId;
        Changed?.Invoke(this, DocumentChangedEventArgs.Content);
    }

    /// <summary>Records that the current content has been persisted to <paramref name="filePath"/>.</summary>
    public void MarkSaved(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = Path.GetFullPath(filePath);
        _fileName = Path.GetFileName(FilePath);
        _savedState = _state;
        Changed?.Invoke(this, DocumentChangedEventArgs.SavedState);
    }

    /// <summary>True when <paramref name="path"/> refers to this document's file (Windows paths are case-insensitive).</summary>
    public bool IsSameFile(string path) =>
        FilePath is not null && string.Equals(FilePath, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);

    private sealed record HistoryEntry(IEditCommand Command, long Before, long After);
}

/// <param name="ContentChanged">True if points or items changed; false if only the saved state or path changed.</param>
public sealed class DocumentChangedEventArgs(bool contentChanged) : EventArgs
{
    public static DocumentChangedEventArgs Content { get; } = new(true);

    public static DocumentChangedEventArgs SavedState { get; } = new(false);

    public bool ContentChanged { get; } = contentChanged;
}
