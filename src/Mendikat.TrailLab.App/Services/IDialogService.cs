using Mendikat.TrailLab.Domain.Documents;

namespace Mendikat.TrailLab.App.Services;

public enum SaveChangesChoice
{
    Save,
    Discard,
    Cancel,
}

/// <summary>User interaction needed by view models; abstracted so the logic can be tested without windows.</summary>
public interface IDialogService
{
    /// <returns>Selected path, or null if the user cancelled.</returns>
    string? AskOpenFilePath(DocumentFormat format);

    /// <returns>Selected path, or null if the user cancelled. The dialog asks before overwriting.</returns>
    string? AskSaveFilePath(DocumentFormat format, string suggestedPath);

    SaveChangesChoice AskSaveChanges(string documentName);

    /// <summary>Shows the modal dialog of a tool view model (simplify, regenerate times).</summary>
    /// <returns>True if the user accepted.</returns>
    bool ShowToolDialog(object viewModel);

    /// <returns>True if the user confirms (EDIT-02).</returns>
    bool Confirm(string message);

    void ShowError(string message);

    void ShowInfo(string message);
}
