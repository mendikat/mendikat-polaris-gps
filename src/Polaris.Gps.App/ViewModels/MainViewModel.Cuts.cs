using Polaris.Gps.App.Resources;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Domain.Processing;
using Microsoft.Extensions.Logging;

namespace Polaris.Gps.App.ViewModels;

/// <summary>
/// Tools › Cut: split the selected track or route at the selected point, trim it before or after the point, and make
/// one track of each segment. Each is one undoable step.
/// </summary>
public sealed partial class MainViewModel
{
    public RelayCommand SplitAtPointCommand { get; private set; } = null!;

    public RelayCommand RemoveBeforeCommand { get; private set; } = null!;

    public RelayCommand RemoveAfterCommand { get; private set; } = null!;

    public RelayCommand SegmentsToTracksCommand { get; private set; } = null!;

    /// <summary>Splits the selected track or route in two at the selected point; the second part goes right after it.</summary>
    public void SplitAtPoint()
    {
        if (SelectedSequencePoint() is not ({ } doc, { } layer, { } point))
        {
            StatusMessage = Strings.StatusCutNeedsPoint;
            return;
        }

        var data = doc.Document.Data;
        (Domain.Editing.IEditCommand Command, string? Name)? result = layer.Model switch
        {
            Track track when TrackCutter.Split(data, track, point) is ({ } command, { } second) => (command, second.Name),
            Route route when TrackCutter.Split(data, route, point) is ({ } command, { } second) => (command, second.Name),
            _ => null,
        };
        if (result is not ({ } edit, var secondName))
        {
            StatusMessage = Strings.StatusSplitAtEnd;
            return;
        }

        doc.Document.Execute(edit);
        doc.SelectPoints([point]);
        StatusMessage = string.Format(Strings.StatusSplit, layer.Name, secondName ?? Strings.UnnamedItem);
        _logger.LogInformation("Split a {Kind} in {FileName}", layer.Kind, doc.FileName);
    }

    /// <summary>Removes the points before the selected one, so the track or route starts there.</summary>
    public void RemoveBefore() => Trim(before: true);

    /// <summary>Removes the points after the selected one, so the track or route ends there.</summary>
    public void RemoveAfter() => Trim(before: false);

    /// <summary>Makes one track of each segment of the selected track.</summary>
    public void SegmentsToTracks()
    {
        if (ActiveDocument is not { SelectedLayer.Model: Track track } doc)
        {
            StatusMessage = Strings.StatusSelectSomething;
            return;
        }

        if (TrackCutter.SegmentsToTracks(doc.Document.Data, track) is not ({ } command, { } created))
        {
            StatusMessage = Strings.StatusSingleSegment;
            return;
        }

        doc.Document.Execute(command);
        StatusMessage = string.Format(Strings.StatusSegmentsToTracks, created.Count + 1);
        _logger.LogInformation("Made {Count} tracks of the segments of a track in {FileName}", created.Count + 1, doc.FileName);
    }

    private void Trim(bool before)
    {
        if (SelectedSequencePoint() is not ({ } doc, { } layer, { } point))
        {
            StatusMessage = Strings.StatusCutNeedsPoint;
            return;
        }

        var result = before ? TrackCutter.RemoveBefore(layer.Model, point) : TrackCutter.RemoveAfter(layer.Model, point);
        if (result is not ({ } command, var removed))
        {
            StatusMessage = Strings.StatusNothingToRemove;
            return;
        }

        if (removed > ConfirmDeleteThreshold && !_dialogs.Confirm(string.Format(Strings.ConfirmDeletePoints, removed)))
        {
            return;
        }

        doc.Document.Execute(command);
        doc.SelectPoints([point]);
        StatusMessage = string.Format(Strings.StatusDeletedPoints, removed);
        _logger.LogInformation("Trimmed a {Kind} in {FileName}: {Count} points removed {Side}", layer.Kind, doc.FileName, removed, before ? "before" : "after");
    }

    /// <summary>The active document, its selected track or route and its only selected point, or null.</summary>
    private (DocumentViewModel Document, LayerItemViewModel Layer, TrailPoint Point)? SelectedSequencePoint() =>
        ActiveDocument is { SelectedLayer: { IsSequence: true } layer, SelectedRows: [var row] } doc ? (doc, layer, row.Point) : null;

    private bool CanCutAtPoint() => !IsBusy && SelectedSequencePoint() is not null;

    private void InitializeCutCommands()
    {
        SplitAtPointCommand = new RelayCommand(SplitAtPoint, CanCutAtPoint);
        RemoveBeforeCommand = new RelayCommand(RemoveBefore, CanCutAtPoint);
        RemoveAfterCommand = new RelayCommand(RemoveAfter, CanCutAtPoint);
        SegmentsToTracksCommand = new RelayCommand(
            SegmentsToTracks, () => !IsBusy && ActiveDocument?.SelectedLayer?.Model is Track { Segments.Count: > 1 });
    }

    private void RefreshCutCommands()
    {
        SplitAtPointCommand.RaiseCanExecuteChanged();
        RemoveBeforeCommand.RaiseCanExecuteChanged();
        RemoveAfterCommand.RaiseCanExecuteChanged();
        SegmentsToTracksCommand.RaiseCanExecuteChanged();
    }
}
