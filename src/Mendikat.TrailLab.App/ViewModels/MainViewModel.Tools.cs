using Mendikat.TrailLab.App.Resources;
using Microsoft.Extensions.Logging;

namespace Mendikat.TrailLab.App.ViewModels;

/// <summary>Processing tools on the selected track or route (increment 6): simplify and regenerate times.</summary>
public sealed partial class MainViewModel
{
    public RelayCommand SimplifyCommand { get; private set; } = null!;

    public RelayCommand RegenerateTimesCommand { get; private set; } = null!;

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

    private void InitializeToolCommands()
    {
        SimplifyCommand = new RelayCommand(Simplify, CanUseTools);
        RegenerateTimesCommand = new RelayCommand(RegenerateTimes, CanUseTools);
    }

    private bool CanUseTools() => !IsBusy && ActiveDocument?.SelectedLayer?.IsSequence == true;

    private void RefreshToolCommands()
    {
        SimplifyCommand.RaiseCanExecuteChanged();
        RegenerateTimesCommand.RaiseCanExecuteChanged();
    }
}
