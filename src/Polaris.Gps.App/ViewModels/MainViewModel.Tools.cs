using Polaris.Gps.App.Resources;
using Polaris.Gps.Domain.Elevation;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Domain.Processing;
using Polaris.Gps.Gpx;
using Polaris.Gps.Mapping.Elevation;
using Microsoft.Extensions.Logging;

namespace Polaris.Gps.App.ViewModels;

/// <summary>Processing tools on the selected track or route: simplify and regenerate times (increment 6), DEM elevations (increment 7).</summary>
public sealed partial class MainViewModel
{
    public RelayCommand SimplifyCommand { get; private set; } = null!;

    public RelayCommand CleanCommand { get; private set; } = null!;

    public RelayCommand RegenerateTimesCommand { get; private set; } = null!;

    public RelayCommand AdjustElevationsCommand { get; private set; } = null!;

    public RelayCommand ReverseCommand { get; private set; } = null!;

    public RelayCommand ConvertToGpxCommand { get; private set; } = null!;

    public RelayCommand JoinTracksCommand { get; private set; } = null!;

    /// <summary>Elevation models by resolution (DEM-01). Set at startup; without it the DEM tool is disabled.</summary>
    public Func<IgnDemResolution, IElevationSource>? ElevationSources { get; init; }

    /// <summary>Douglas–Peucker with preview and confirmation; one undoable step (SIMP-04).</summary>
    public void Simplify()
    {
        if (ActiveDocument is not { SelectedLayer: { IsSequence: true } layer } doc)
        {
            StatusMessage = Strings.StatusSelectSomething;
            return;
        }

        var dialog = new SimplifyViewModel(layer);
        if (!_dialogs.ShowToolDialog(dialog) || dialog.Plan is not { Command: { } command } plan)
        {
            return;
        }

        doc.Document.Execute(command);
        StatusMessage = string.Format(Strings.StatusSimplified, layer.Name, plan.OriginalCount, plan.ResultCount);
        _logger.LogInformation(
            "Simplified a {Kind} in {FileName}: {Original} -> {Result} points, tolerance {Tolerance} m",
            layer.Kind, doc.FileName, plan.OriginalCount, plan.ResultCount, plan.ToleranceMeters);
    }

    /// <summary>Removes GPS jumps, spikes and stops and smooths the elevation, with preview; one undoable step.</summary>
    public void Clean()
    {
        if (ActiveDocument is not { SelectedLayer: { IsSequence: true } layer } doc)
        {
            StatusMessage = Strings.StatusSelectSomething;
            return;
        }

        var dialog = new CleanViewModel(layer);
        if (!_dialogs.ShowToolDialog(dialog) || dialog.Plan is not { Command: { } command } plan)
        {
            return;
        }

        doc.Document.Execute(command);
        StatusMessage = string.Format(Strings.StatusCleaned, layer.Name, plan.Removed.ToString("N0", Spanish), plan.Smoothed.ToString("N0", Spanish));
        _logger.LogInformation(
            "Cleaned a {Kind} in {FileName}: {Jumps} jumps, {Stops} stop points removed, {Smoothed} elevations smoothed",
            layer.Kind, doc.FileName, plan.Jumps, plan.StopPoints, plan.Smoothed);
    }

    /// <summary>New times from a start time and a speed or interval, with preview; one undoable step (TIME-06).</summary>
    public void RegenerateTimes()
    {
        if (ActiveDocument is not { SelectedLayer: { IsSequence: true } layer } doc)
        {
            StatusMessage = Strings.StatusSelectSomething;
            return;
        }

        var dialog = new TimesViewModel(layer);
        if (!_dialogs.ShowToolDialog(dialog) || dialog.Plan is not { } plan)
        {
            return;
        }

        doc.Document.Execute(plan.Command);
        StatusMessage = string.Format(Strings.StatusTimesRegenerated, layer.Name, plan.PointCount);
        _logger.LogInformation("Regenerated times of a {Kind} in {FileName}: {Count} points", layer.Kind, doc.FileName, plan.PointCount);
    }

    /// <summary>
    /// Elevations from the IGN terrain model, with preview and confirmation (DEM-02/03); one undoable step.
    /// Points without DEM data keep their elevation (DEM-04).
    /// </summary>
    public void AdjustElevations()
    {
        if (ActiveDocument is not { SelectedLayer: { IsSequence: true } layer } doc || ElevationSources is null)
        {
            StatusMessage = Strings.StatusSelectSomething;
            return;
        }

        var dialog = new DemViewModel(layer, ElevationSources, LocalDems);
        if (!_dialogs.ShowToolDialog(dialog) || dialog.Plan is not { Command: { } command } plan)
        {
            return;
        }

        doc.Document.Execute(command);
        StatusMessage = string.Format(Strings.StatusElevationsAdjusted, layer.Name, plan.Changed);
        _logger.LogInformation(
            "Adjusted elevations of a {Kind} in {FileName} with {Source}: {Changed} changed, {NoData} without data",
            layer.Kind, doc.FileName, dialog.Resolution.DisplayName, plan.Changed, plan.NoData);
    }

    /// <summary>Reverses the selected track or route, mirroring its times; one undoable step, no dialog (REV-03).</summary>
    public void Reverse()
    {
        if (ActiveDocument is not { SelectedLayer: { IsSequence: true } layer } doc)
        {
            StatusMessage = Strings.StatusSelectSomething;
            return;
        }

        doc.Document.Execute(layer.Model switch
        {
            Track track => TrackReverser.Reverse(track),
            Route route => TrackReverser.Reverse(route),
            _ => throw new InvalidOperationException("Only tracks and routes can be reversed."),
        });
        StatusMessage = string.Format(Strings.StatusReversed, layer.Name, layer.PointCount);
        _logger.LogInformation("Reversed a {Kind} in {FileName}: {Count} points", layer.Kind, doc.FileName, layer.PointCount);
    }

    /// <summary>
    /// Joins two or more tracks of the open documents by proximity, with preview. The joined track opens in a new
    /// unsaved GPX document; the original tracks do not change.
    /// </summary>
    public void JoinTracks()
    {
        var candidates = JoinableTracks().ToList();
        if (candidates.Count < 2)
        {
            StatusMessage = Strings.StatusJoinNeedsTracks;
            return;
        }

        var dialog = new JoinTracksViewModel(candidates.Select(c => (c.Track, c.Document.FileName)));
        if (!_dialogs.ShowToolDialog(dialog) || dialog.Plan is not { } plan)
        {
            return;
        }

        var track = dialog.Build();
        var sources = plan.Steps.Select(s => candidates.First(c => ReferenceEquals(c.Track, plan.Tracks[s.Index])).Document).ToList();

        // GPX 1.0 fields kept in the points are upgraded when saving, as for a GPX 1.0 file; there is nothing to ask.
        var fromGpx10 = sources.Any(d => d.Document.Data.FormatState is GpxFormatState { SourceVersion: "1.0" });
        var data = new TrailData { FormatState = new GpxFormatState { SourceVersion = fromGpx10 ? "1.0" : "1.1" } };
        data.Tracks.Add(track);
        var viewModel = AddNewDocument(data);
        viewModel.GpxUpgradeAccepted = fromGpx10;
        viewModel.Document.MarkChanged(); // it exists only in memory: closing it asks to save

        StatusMessage = string.Format(
            Strings.StatusJoined, plan.Steps.Count, viewModel.Layers[0].Name, track.PointCount.ToString("N0", Spanish), track.Segments.Count);
        _logger.LogInformation(
            "Joined {Count} tracks into {FileName}: {Points} points, {Segments} segments, {Reversed} reversed, gaps {Gap:0} m (max {MaxGap} m)",
            plan.Steps.Count, viewModel.FileName, track.PointCount, track.Segments.Count, plan.ReversedCount, plan.TotalGapMeters, plan.MaxGapMeters);
    }

    /// <summary>Tracks with points of every open document, in tab order.</summary>
    private IEnumerable<(Track Track, DocumentViewModel Document)> JoinableTracks() =>
        Documents.SelectMany(d => d.Document.Data.Tracks.Where(t => t.PointCount > 0).Select(t => (t, d)));

    /// <summary>Asks for KML, KMZ, TCX, OziExplorer or CompeGPS/TwoNav files and converts them to GPX files in a dialog with a report (CONV-06).</summary>
    public void ConvertToGpx()
    {
        if (_dialogs.AskConvertFilePaths() is not { Count: > 0 } files)
        {
            return;
        }

        var dialog = new ConvertViewModel(_dialogs, files);
        _dialogs.ShowToolDialog(dialog);
        if (dialog.Converted + dialog.Skipped + dialog.Failed > 0)
        {
            StatusMessage = dialog.StatusSummary;
            _logger.LogInformation(
                "Converted to GPX: {Converted} converted, {Skipped} skipped, {Failed} failed",
                dialog.Converted, dialog.Skipped, dialog.Failed);
        }
    }

    private void InitializeToolCommands()
    {
        ConvertToGpxCommand = new RelayCommand(ConvertToGpx, () => !IsBusy);
        ReverseCommand = new RelayCommand(Reverse, CanUseTools);
        JoinTracksCommand = new RelayCommand(JoinTracks, () => !IsBusy && JoinableTracks().Skip(1).Any());
        SimplifyCommand = new RelayCommand(Simplify, CanUseTools);
        CleanCommand = new RelayCommand(Clean, CanUseTools);
        RegenerateTimesCommand = new RelayCommand(RegenerateTimes, CanUseTools);
        AdjustElevationsCommand = new RelayCommand(AdjustElevations, () => CanUseTools() && ElevationSources is not null);
    }

    private bool CanUseTools() => !IsBusy && ActiveDocument?.SelectedLayer?.IsSequence == true;

    private void RefreshToolCommands()
    {
        SimplifyCommand.RaiseCanExecuteChanged();
        CleanCommand.RaiseCanExecuteChanged();
        RegenerateTimesCommand.RaiseCanExecuteChanged();
        AdjustElevationsCommand.RaiseCanExecuteChanged();
        ReverseCommand.RaiseCanExecuteChanged();
        JoinTracksCommand.RaiseCanExecuteChanged();
        ConvertToGpxCommand.RaiseCanExecuteChanged();
        RefreshCutCommands();
        RefreshDrawingCommands();
        RefreshCommunicationCommands();
    }
}
