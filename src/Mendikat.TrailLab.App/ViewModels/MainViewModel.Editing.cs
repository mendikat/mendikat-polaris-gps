using Mendikat.TrailLab.App.Resources;
using Mendikat.TrailLab.Domain.Editing;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Domain.Model;
using Microsoft.Extensions.Logging;

namespace Mendikat.TrailLab.App.ViewModels;

/// <summary>Point and item editing on the active document (EDIT-01..07, WPE-02/03). Every edit can be undone.</summary>
public sealed partial class MainViewModel
{
    /// <summary>Deleting more points than this at once asks for confirmation (EDIT-02).</summary>
    public const int ConfirmDeleteThreshold = 50;

    public RelayCommand DeleteCommand { get; private set; } = null!;

    public RelayCommand InsertPointCommand { get; private set; } = null!;

    public RelayCommand UndoCommand { get; private set; } = null!;

    public RelayCommand RedoCommand { get; private set; } = null!;

    public RelayCommand ApplyPointCommand { get; private set; } = null!;

    public RelayCommand RevertPointCommand { get; private set; } = null!;

    /// <summary>
    /// Supr: deletes the selected points; with no point selected, the selected track or route (EDIT-01).
    /// Confirms only for a whole track/route or more than <see cref="ConfirmDeleteThreshold"/> points (EDIT-02).
    /// </summary>
    public void DeleteSelection()
    {
        var doc = ActiveDocument;
        if (doc is null)
        {
            return;
        }

        if (doc.SelectedRows.Count > 0)
        {
            DeletePoints(doc);
        }
        else if (doc.SelectedLayer is { Kind: not LayerKind.Waypoints } layer)
        {
            DeleteItem(doc, layer);
        }
        else
        {
            StatusMessage = Strings.StatusSelectSomething;
        }
    }

    /// <summary>Insert: adds the geodesic midpoint after the selected point (before it if it is the last), EDIT-03.</summary>
    public void InsertPoint()
    {
        var doc = ActiveDocument;
        if (doc is null)
        {
            return;
        }

        if (doc.SelectedRows.Count != 1 || doc.SelectedLayer?.IsSequence != true)
        {
            StatusMessage = Strings.StatusInsertNeedsPoint;
            return;
        }

        var row = doc.SelectedRows[0];
        var command = PointEditing.CreateInsertion(row.Owner, row.Point);
        if (command is null)
        {
            StatusMessage = Strings.StatusInsertTooFewPoints;
            return;
        }

        doc.Document.Execute(command);
        doc.SelectPoints([command.Point]);
        StatusMessage = Strings.StatusInserted;
        _logger.LogInformation("Inserted a point in {FileName}", doc.FileName);
    }

    /// <summary>
    /// Ctrl+click on the map (APP-08): adds a point at <paramref name="position"/> to the active document, right after
    /// the selected point, or at the end of the selected track, route or waypoint list. The new point is selected, so
    /// consecutive clicks draw a line. Undoable like any edit.
    /// </summary>
    public void AddPointAt(GeoPosition position)
    {
        var doc = ActiveDocument;
        if (IsBusy || doc is null)
        {
            return;
        }

        var after = doc.SelectedRows.Count == 1 ? doc.SelectedRows[0].Point : null;
        var (command, point) = PointEditing.CreateAddition(doc.Document.Data, doc.SelectedLayer?.Model, after, position);
        doc.Document.Execute(command);
        doc.SelectPoints([point]);
        StatusMessage = Strings.StatusPointAdded;
        _logger.LogInformation("Added a point in {FileName}", doc.FileName);
    }

    /// <summary>Moves a point to a new position, e.g. after dragging it on the map (EDIT-04).</summary>
    public void MovePoint(DocumentViewModel doc, TrailPoint point, GeoPosition position)
    {
        if (IsBusy || point.Position == position)
        {
            return;
        }

        doc.Document.Execute(new EditPointCommand(point, PointValues.From(point) with { Position = position }));
        doc.SelectPoints([point]);
        StatusMessage = Strings.StatusMoved;
    }

    /// <summary>Applies the values typed in the detail panel as one undoable step; invalid values are not applied (WPE-03).</summary>
    public void ApplyPointDetail()
    {
        var doc = ActiveDocument;
        if (doc?.Detail is not { } detail || !detail.TryBuild(out var values))
        {
            return;
        }

        if (values == PointValues.From(detail.Point))
        {
            return;
        }

        doc.Document.Execute(new EditPointCommand(detail.Point, values));
        StatusMessage = Strings.StatusPointUpdated;
    }

    /// <summary>Selects a point clicked on the map, or clears the selection when <paramref name="point"/> is null (LAY-03).</summary>
    public void SelectFromMap(DocumentViewModel doc, TrailPoint? point)
    {
        if (point is null)
        {
            doc.SetSelectedRows([]);
            return;
        }

        ActiveDocument = doc;
        doc.SelectPoints([point]);
    }

    public void Undo()
    {
        if (ActiveDocument?.Document.Undo() == true)
        {
            StatusMessage = Strings.StatusUndone;
        }
    }

    public void Redo()
    {
        if (ActiveDocument?.Document.Redo() == true)
        {
            StatusMessage = Strings.StatusRedone;
        }
    }

    private void InitializeEditingCommands()
    {
        DeleteCommand = new RelayCommand(DeleteSelection, () => !IsBusy && ActiveDocument is not null);
        InsertPointCommand = new RelayCommand(InsertPoint, () => !IsBusy && ActiveDocument is not null);
        UndoCommand = new RelayCommand(Undo, () => !IsBusy && ActiveDocument?.CanUndo == true);
        RedoCommand = new RelayCommand(Redo, () => !IsBusy && ActiveDocument?.CanRedo == true);
        ApplyPointCommand = new RelayCommand(ApplyPointDetail, () => !IsBusy && ActiveDocument?.Detail is not null);
        RevertPointCommand = new RelayCommand(() => ActiveDocument?.RevertDetail(), () => ActiveDocument?.Detail is not null);
    }

    private void RefreshEditingCommands()
    {
        DeleteCommand.RaiseCanExecuteChanged();
        InsertPointCommand.RaiseCanExecuteChanged();
        UndoCommand.RaiseCanExecuteChanged();
        RedoCommand.RaiseCanExecuteChanged();
        ApplyPointCommand.RaiseCanExecuteChanged();
        RevertPointCommand.RaiseCanExecuteChanged();
    }

    private void DeletePoints(DocumentViewModel doc)
    {
        var rows = doc.SelectedRows;
        if (rows.Count > ConfirmDeleteThreshold && !_dialogs.Confirm(string.Format(Strings.ConfirmDeletePoints, rows.Count)))
        {
            return;
        }

        // The point that follows the last deleted one in the list is selected afterwards (ACC-EDIT-01).
        var deleted = rows.Select(r => r.Point).ToHashSet(ReferenceEqualityComparer.Instance);
        var lastIndex = -1;
        for (var i = 0; i < doc.Rows.Count; i++)
        {
            if (deleted.Contains(doc.Rows[i].Point))
            {
                lastIndex = i;
            }
        }

        var next = lastIndex + 1 < doc.Rows.Count ? doc.Rows[lastIndex + 1].Point : null;

        var commands = rows.GroupBy(r => r.Owner)
            .Select(g => (IEditCommand)new RemovePointsCommand(g.Key, g.Select(r => r.Point)))
            .ToList();
        doc.Document.Execute(commands.Count == 1 ? commands[0] : new CompositeCommand(commands));
        if (next is not null)
        {
            doc.SelectPoints([next]);
        }

        StatusMessage = string.Format(Strings.StatusDeletedPoints, rows.Count);
        _logger.LogInformation("Deleted {Count} points in {FileName}", rows.Count, doc.FileName);
    }

    private void DeleteItem(DocumentViewModel doc, LayerItemViewModel layer)
    {
        var template = layer.Kind == LayerKind.Track ? Strings.ConfirmDeleteTrack : Strings.ConfirmDeleteRoute;
        if (!_dialogs.Confirm(string.Format(template, layer.Name, layer.PointCount)))
        {
            return;
        }

        var name = layer.Name;
        IEditCommand command = layer.Model switch
        {
            Track track => new RemoveItemCommand<Track>(doc.Document.Data.Tracks, track),
            Route route => new RemoveItemCommand<Route>(doc.Document.Data.Routes, route),
            _ => throw new InvalidOperationException("Only tracks and routes can be deleted as a whole."),
        };
        doc.Document.Execute(command);
        StatusMessage = string.Format(Strings.StatusDeletedItem, name);
        _logger.LogInformation("Deleted a {Kind} in {FileName}", layer.Kind, doc.FileName);
    }
}
