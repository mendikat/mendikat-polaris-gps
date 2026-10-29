using Mendikat.TrailLab.App.Resources;
using Mendikat.TrailLab.Domain.Editing;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Domain.Model;
using Microsoft.Extensions.Logging;

namespace Mendikat.TrailLab.App.ViewModels;

/// <summary>Adding, editing and deleting waypoints of the active document. Every change can be undone.</summary>
public sealed partial class MainViewModel
{
    public RelayCommand EditWaypointCommand { get; private set; } = null!;

    /// <summary>One or more waypoints are selected.</summary>
    public bool HasWaypointSelected =>
        !IsBusy && ActiveDocument is { SelectedRows.Count: > 0, SelectedLayer.Kind: LayerKind.Waypoints };

    /// <summary>Asks for the name, comment and description and adds a waypoint at <paramref name="position"/>.</summary>
    public void AddWaypointAt(GeoPosition position)
    {
        var doc = ActiveDocument;
        if (IsBusy || doc is null)
        {
            return;
        }

        var waypoints = doc.Document.Data.Waypoints;
        var dialog = new WaypointViewModel(Strings.WaypointNewTitle, position,
            string.Format(Strings.WaypointDefaultName, waypoints.Count + 1), null, null);
        if (!_dialogs.ShowToolDialog(dialog))
        {
            return;
        }

        var point = new TrailPoint(position)
        {
            Name = WaypointViewModel.Clean(dialog.Name),
            Comment = WaypointViewModel.Clean(dialog.Comment),
            Description = WaypointViewModel.Clean(dialog.Description),
            IsAdded = true,
        };
        doc.Document.Execute(new InsertPointCommand(waypoints, waypoints.Count, point));
        doc.SelectPoints([point]);
        StatusMessage = string.Format(Strings.StatusWaypointAdded, point.Name ?? string.Empty);
        _logger.LogInformation("Added a waypoint in {FileName}", doc.FileName);
    }

    /// <summary>Edits the name, comment and description of the selected waypoint as one undoable step.</summary>
    public void EditWaypoint()
    {
        var doc = ActiveDocument;
        if (doc is not { SelectedRows.Count: 1, SelectedLayer.Kind: LayerKind.Waypoints })
        {
            return;
        }

        var point = doc.SelectedRows[0].Point;
        var dialog = new WaypointViewModel(Strings.WaypointEditTitle, point.Position, point.Name, point.Comment, point.Description);
        if (!_dialogs.ShowToolDialog(dialog))
        {
            return;
        }

        var values = PointValues.From(point) with
        {
            Name = WaypointViewModel.Clean(dialog.Name),
            Comment = WaypointViewModel.Clean(dialog.Comment),
            Description = WaypointViewModel.Clean(dialog.Description),
        };
        if (values == PointValues.From(point))
        {
            return;
        }

        doc.Document.Execute(new EditPointCommand(point, values));
        StatusMessage = Strings.StatusWaypointUpdated;
    }

    /// <summary>Deleting waypoints always asks first.</summary>
    private bool ConfirmWaypointDeletion(DocumentViewModel doc)
    {
        var rows = doc.SelectedRows;
        var message = rows.Count == 1
            ? string.Format(Strings.ConfirmDeleteWaypoint, rows[0].Point.Name ?? rows[0].Number)
            : string.Format(Strings.ConfirmDeleteWaypoints, rows.Count);
        return _dialogs.Confirm(message);
    }

    private void InitializeWaypointCommands() =>
        EditWaypointCommand = new RelayCommand(EditWaypoint,
            () => !IsBusy && ActiveDocument is { SelectedRows.Count: 1, SelectedLayer.Kind: LayerKind.Waypoints });
}
