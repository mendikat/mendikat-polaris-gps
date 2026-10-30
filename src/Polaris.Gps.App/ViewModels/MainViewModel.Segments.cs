using Polaris.Gps.App.Resources;
using Polaris.Gps.Domain.Editing;
using Polaris.Gps.Domain.Model;
using Microsoft.Extensions.Logging;

namespace Polaris.Gps.App.ViewModels;

/// <summary>The segments of a track listed in the layers panel: select one (its points) or delete it.</summary>
public sealed partial class MainViewModel
{
    public RelayCommand SelectSegmentCommand { get; private set; } = null!;

    public RelayCommand DeleteSegmentCommand { get; private set; } = null!;

    /// <summary>Selects every point of the segment, so it is highlighted on the map and in the list.</summary>
    public void SelectSegment(SegmentItemViewModel segment)
    {
        if (DocumentOf(segment) is not { } doc)
        {
            return;
        }

        ActiveDocument = doc;
        doc.SelectPoints(segment.Segment.Points);
    }

    /// <summary>
    /// Deletes one segment of a track after confirmation; the other segments stay. One undoable step. The panel only
    /// offers it on tracks with more than one segment, so a track never loses its last one this way.
    /// </summary>
    public void DeleteSegment(SegmentItemViewModel segment)
    {
        if (IsBusy || DocumentOf(segment) is not { } doc || segment.Layer.Model is not Track track || track.Segments.Count < 2)
        {
            return;
        }

        var number = segment.Index + 1;
        if (!_dialogs.Confirm(string.Format(Strings.ConfirmDeleteSegment, number, segment.Layer.Name, segment.Segment.Points.Count.ToString("N0", Spanish))))
        {
            return;
        }

        doc.Document.Execute(new RemoveItemCommand<TrackSegment>(track.Segments, segment.Segment));
        StatusMessage = string.Format(Strings.StatusSegmentDeleted, number, segment.Layer.Name);
        _logger.LogInformation("Deleted segment {Number} of a track in {FileName}", number, doc.FileName);
    }

    private DocumentViewModel? DocumentOf(SegmentItemViewModel segment) =>
        Documents.FirstOrDefault(d => d.Layers.Contains(segment.Layer));

    private void InitializeSegmentCommands()
    {
        SelectSegmentCommand = new RelayCommand(p =>
        {
            if (p is SegmentItemViewModel segment)
            {
                SelectSegment(segment);
            }

            return Task.CompletedTask;
        });
        DeleteSegmentCommand = new RelayCommand(p =>
        {
            if (p is SegmentItemViewModel segment)
            {
                DeleteSegment(segment);
            }

            return Task.CompletedTask;
        }, _ => !IsBusy);
    }
}
