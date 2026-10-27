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

    /// <summary>Files to convert to GPX (KML, KMZ, TCX; several can be chosen, CONV-06).</summary>
    /// <returns>Selected paths, or null if the user cancelled.</returns>
    IReadOnlyList<string>? AskConvertFilePaths();

    /// <returns>Selected folder, or null if the user cancelled.</returns>
    string? AskFolder(string? initialFolder);

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

    /// <summary>Opens a web page, an e-mail address (mailto:) or a local file with the default program of Windows.</summary>
    void OpenExternal(string target);

    /// <summary>Shows the modal "About" window (APP-09).</summary>
    void ShowAbout(ViewModels.AboutViewModel about);
}
