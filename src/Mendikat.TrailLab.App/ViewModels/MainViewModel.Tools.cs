using Mendikat.TrailLab.App.Resources;
using Mendikat.TrailLab.Domain.Elevation;
using Mendikat.TrailLab.Domain.Model;
using Mendikat.TrailLab.Domain.Processing;
using Mendikat.TrailLab.Mapping.Elevation;
using Microsoft.Extensions.Logging;

namespace Mendikat.TrailLab.App.ViewModels;

/// <summary>Processing tools on the selected track or route: simplify and regenerate times (increment 6), DEM elevations (increment 7).</summary>
public sealed partial class MainViewModel
{
    public RelayCommand SimplifyCommand { get; private set; } = null!;

    public RelayCommand RegenerateTimesCommand { get; private set; } = null!;

    public RelayCommand AdjustElevationsCommand { get; private set; } = null!;

    public RelayCommand ReverseCommand { get; private set; } = null!;

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

        var dialog = new DemViewModel(layer, ElevationSources);
        if (!_dialogs.ShowToolDialog(dialog) || dialog.Plan is not { Command: { } command } plan)
        {
            return;
        }

        doc.Document.Execute(command);
        StatusMessage = string.Format(Strings.StatusElevationsAdjusted, layer.Name, plan.Changed);
        _logger.LogInformation(
            "Adjusted elevations of a {Kind} in {FileName} with {Source}: {Changed} changed, {NoData} without data",
            layer.Kind, doc.FileName, dialog.Resolution.Resolution, plan.Changed, plan.NoData);
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

    private void InitializeToolCommands()
    {
        ReverseCommand = new RelayCommand(Reverse, CanUseTools);
        SimplifyCommand = new RelayCommand(Simplify, CanUseTools);
        RegenerateTimesCommand = new RelayCommand(RegenerateTimes, CanUseTools);
        AdjustElevationsCommand = new RelayCommand(AdjustElevations, () => CanUseTools() && ElevationSources is not null);
    }

    private bool CanUseTools() => !IsBusy && ActiveDocument?.SelectedLayer?.IsSequence == true;

    private void RefreshToolCommands()
    {
        SimplifyCommand.RaiseCanExecuteChanged();
        RegenerateTimesCommand.RaiseCanExecuteChanged();
        AdjustElevationsCommand.RaiseCanExecuteChanged();
        ReverseCommand.RaiseCanExecuteChanged();
    }
}
