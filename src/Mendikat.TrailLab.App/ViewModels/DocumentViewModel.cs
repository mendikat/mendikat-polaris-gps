using System.Collections.ObjectModel;
using System.Globalization;
using Mendikat.TrailLab.App.Resources;
using Mendikat.TrailLab.App.Services;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Model;
using Mendikat.TrailLab.Mapping.Layers;

namespace Mendikat.TrailLab.App.ViewModels;

/// <summary>
/// Presentation state of one open document (one tab): layers (LAY-01), points of the selected layer,
/// point selection (LAY-03) and the detail of the selected point (LAY-04). Rebuilt after every edit (EDIT-07).
/// </summary>
public sealed class DocumentViewModel : ObservableObject
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private LayerItemViewModel? _selectedLayer;
    private IReadOnlyList<PointRowViewModel> _rows = [];
    private IReadOnlyList<PointRowViewModel> _selectedRows = [];
    private PointDetailViewModel? _detail;

    public DocumentViewModel(TrailDocument document, int colorIndex = 0)
    {
        Document = document;
        ColorIndex = colorIndex;
        FieldSupport = PointFieldSupport.For(document.Data);
        SyncLayers();
        SelectedLayer = Layers.FirstOrDefault();
        Document.Changed += OnDocumentChanged;
    }

    /// <summary>Raised after the view model has been updated for an edit, undo or redo; the map redraws.</summary>
    public event EventHandler? ContentChanged;

    /// <summary>Raised when the selected points change; the map updates its highlight.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Raised when a layer is shown or hidden.</summary>
    public event EventHandler<LayerItemViewModel>? LayerVisibilityChanged;

    public TrailDocument Document { get; }

    /// <summary>Index into <see cref="MapStyles.DocumentColors"/>; the same color is used on the map and in the tab.</summary>
    public int ColorIndex { get; }

    public PointFieldSupport FieldSupport { get; }

    /// <summary>The user already accepted that this GPX 1.0 document is saved as GPX 1.1 (asked once).</summary>
    public bool GpxUpgradeAccepted { get; set; }

    public string ColorHex
    {
        get
        {
            var c = MapStyles.ColorFor(ColorIndex);
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
    }

    public string FileName => Document.FileName;

    /// <summary>Full path of the file; null for a new document not saved yet (APP-08).</summary>
    public string? FilePath => Document.FilePath;

    public bool IsDirty => Document.IsDirty;

    /// <summary>Tab caption: file name plus "*" when there are unsaved changes (SAVE-04).</summary>
    public string Header => Document.IsDirty ? $"{Document.FileName} *" : Document.FileName;

    public string FormatLabel => UserMessages.SourceFormat(Document.Format, Document.Data);

    public string Counts
    {
        get
        {
            var data = Document.Data;
            var points = data.Tracks.Sum(t => t.PointCount) + data.Routes.Sum(r => r.Points.Count) + data.Waypoints.Count;
            return string.Format(Spanish, Strings.SummaryCounts, data.Tracks.Count, data.Routes.Count, data.Waypoints.Count, points.ToString("N0", Spanish));
        }
    }

    public IReadOnlyList<string> Warnings => Document.Warnings.Select(UserMessages.ForWarning).ToList();

    public bool HasWarnings => Document.Warnings.Count > 0;

    public ObservableCollection<LayerItemViewModel> Layers { get; } = [];

    /// <summary>Layer whose points are listed. Changing it clears the point selection.</summary>
    public LayerItemViewModel? SelectedLayer
    {
        get => _selectedLayer;
        set
        {
            if (SetProperty(ref _selectedLayer, value))
            {
                Rows = BuildRows(value);
                SetSelectedRows([]);
            }
        }
    }

    public IReadOnlyList<PointRowViewModel> Rows
    {
        get => _rows;
        private set => SetProperty(ref _rows, value);
    }

    /// <summary>Selected rows (several for multi-point deletion). The view keeps the list control in sync.</summary>
    public IReadOnlyList<PointRowViewModel> SelectedRows
    {
        get => _selectedRows;
        private set => SetProperty(ref _selectedRows, value);
    }

    public IEnumerable<TrailPoint> SelectedPoints => SelectedRows.Select(r => r.Point);

    /// <summary>Editable detail when exactly one point is selected.</summary>
    public PointDetailViewModel? Detail
    {
        get => _detail;
        private set => SetProperty(ref _detail, value);
    }

    /// <summary>Hint shown in the detail area when no single point is selected.</summary>
    public string SelectionHint => SelectedRows.Count > 1
        ? string.Format(Spanish, Strings.SeveralPointsSelected, SelectedRows.Count)
        : Strings.NoPointSelected;

    public bool CanUndo => Document.CanUndo;

    public bool CanRedo => Document.CanRedo;

    /// <summary>Called by the view when the user changes the selection in the list.</summary>
    public void SetSelectedRows(IEnumerable<PointRowViewModel> rows)
    {
        var selection = rows.ToList();
        if (selection.SequenceEqual(SelectedRows))
        {
            return;
        }

        SelectedRows = selection;
        Detail = selection.Count == 1 ? new PointDetailViewModel(selection[0].Point, FieldSupport) : null;
        OnPropertyChanged(nameof(SelectionHint));
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Selects points (e.g. clicked on the map): shows the layer that contains the first one and selects its rows.</summary>
    public void SelectPoints(IReadOnlyCollection<TrailPoint> points)
    {
        var first = points.FirstOrDefault();
        if (first is null)
        {
            SetSelectedRows([]);
            return;
        }

        var location = Document.Data.Locate(first);
        var layer = location is null ? null : Layers.FirstOrDefault(l => ReferenceEquals(l.Model, location.Item));
        if (layer is null)
        {
            return;
        }

        SelectedLayer = layer;
        var wanted = points.ToHashSet(ReferenceEqualityComparer.Instance);
        SetSelectedRows(Rows.Where(r => wanted.Contains(r.Point)));
    }

    /// <summary>Discards the edits typed in the detail panel.</summary>
    public void RevertDetail()
    {
        if (SelectedRows.Count == 1)
        {
            Detail = new PointDetailViewModel(SelectedRows[0].Point, FieldSupport);
        }
    }

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        if (!e.ContentChanged)
        {
            return;
        }

        var previousSelection = SelectedPoints.ToList();
        SyncLayers();
        if (SelectedLayer is not null && !Layers.Contains(SelectedLayer))
        {
            SelectedLayer = Layers.FirstOrDefault();
        }
        else
        {
            Rows = BuildRows(SelectedLayer);
            var stillPresent = previousSelection.ToHashSet(ReferenceEqualityComparer.Instance);
            _selectedRows = [];
            SetSelectedRows(Rows.Where(r => stillPresent.Contains(r.Point)));
            RevertDetail();
        }

        foreach (var layer in Layers)
        {
            layer.Refresh();
        }

        OnPropertyChanged(nameof(Counts));
        ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Makes <see cref="Layers"/> match the document, keeping existing items (and their visibility) by model.</summary>
    private void SyncLayers()
    {
        var data = Document.Data;
        var wanted = data.Tracks.Select(t => (LayerKind.Track, (object)t))
            .Concat(data.Routes.Select(r => (LayerKind.Route, (object)r)))
            .Concat(data.Waypoints.Count > 0 ? [(LayerKind.Waypoints, (object)data.Waypoints)] : [])
            .ToList();

        // Incremental update (no Clear): resetting the collection would make the list control drop its selection.
        var wantedModels = wanted.Select(w => w.Item2).ToHashSet(ReferenceEqualityComparer.Instance);
        foreach (var stale in Layers.Where(l => !wantedModels.Contains(l.Model)).ToList())
        {
            Layers.Remove(stale);
        }

        for (var i = 0; i < wanted.Count; i++)
        {
            var (kind, model) = wanted[i];
            var current = Layers.Select((l, index) => (l, index)).FirstOrDefault(x => ReferenceEquals(x.l.Model, model));
            if (current.l is null)
            {
                var item = new LayerItemViewModel(kind, model);
                item.VisibilityChanged += (s, _) => LayerVisibilityChanged?.Invoke(this, (LayerItemViewModel)s!);
                Layers.Insert(i, item);
            }
            else if (current.index != i)
            {
                Layers.Move(current.index, i);
            }
        }
    }

    private static List<PointRowViewModel> BuildRows(LayerItemViewModel? layer)
    {
        if (layer is null)
        {
            return [];
        }

        var lists = layer.PointLists.ToList();
        var showSegment = lists.Count > 1;
        var rows = new List<PointRowViewModel>(layer.PointCount);
        foreach (var (points, segment) in lists)
        {
            for (var i = 0; i < points.Count; i++)
            {
                rows.Add(new PointRowViewModel(points[i], points, i, segment, showSegment));
            }
        }

        return rows;
    }
}
