using Polaris.Gps.App.Services;
using Polaris.Gps.App.ViewModels;
using Polaris.Gps.Domain.Documents;

namespace Polaris.Gps.Tests.Fakes;

/// <summary>Scripted user answers; records what was shown.</summary>
internal sealed class FakeDialogService : IDialogService
{
    public Queue<string?> OpenPaths { get; } = new();

    public string? SavePath { get; set; }

    public SaveChangesChoice SaveChangesAnswer { get; set; } = SaveChangesChoice.Cancel;

    public List<string> SaveChangesQuestions { get; } = [];

    public List<string> Errors { get; } = [];

    public List<string> Infos { get; } = [];

    public string? AskOpenFilePath(DocumentFormat format) => OpenPaths.Count > 0 ? OpenPaths.Dequeue() : null;

    /// <summary>Answers to "files to convert", in order; null when empty (the user cancelled).</summary>
    public Queue<IReadOnlyList<string>?> ConvertPaths { get; } = new();

    public IReadOnlyList<string>? AskConvertFilePaths() => ConvertPaths.Count > 0 ? ConvertPaths.Dequeue() : null;

    /// <summary>Answers to "open cartography", in order; null when empty (the user cancelled).</summary>
    public Queue<IReadOnlyList<string>?> LocalMapPaths { get; } = new();

    public IReadOnlyList<string>? AskLocalMapPaths() => LocalMapPaths.Count > 0 ? LocalMapPaths.Dequeue() : null;

    public string? EcwpUrl { get; set; }

    public string? AskEcwpUrl() => EcwpUrl;

    /// <summary>The user's answer to the ECW licence; counts how often it was shown.</summary>
    public bool EcwLicenseAnswer { get; set; }

    public int EcwLicenseShown { get; private set; }

    public bool AskEcwLicense()
    {
        EcwLicenseShown++;
        return EcwLicenseAnswer;
    }

    public string? Folder { get; set; }

    public string? AskFolder(string? initialFolder) => Folder;

    public string? SuggestedSavePath { get; private set; }

    public string? AskSaveFilePath(DocumentFormat format, string suggestedPath)
    {
        SuggestedSavePath = suggestedPath;
        return SavePath;
    }

    public SaveChangesChoice AskSaveChanges(string documentName)
    {
        SaveChangesQuestions.Add(documentName);
        return SaveChangesAnswer;
    }

    /// <summary>Simulates the user in a tool dialog: can change the view model and returns whether it was accepted.</summary>
    public Func<object, bool> ToolDialog { get; set; } = _ => false;

    public bool ShowToolDialog(object viewModel) => ToolDialog(viewModel);

    public bool ConfirmAnswer { get; set; } = true;

    public List<string> Confirmations { get; } = [];

    public bool Confirm(string message)
    {
        Confirmations.Add(message);
        return ConfirmAnswer;
    }

    public void ShowError(string message) => Errors.Add(message);

    public void ShowInfo(string message) => Infos.Add(message);

    public List<string> OpenedExternally { get; } = [];

    public void OpenExternal(string target) => OpenedExternally.Add(target);

    public AboutViewModel? AboutShown { get; private set; }

    public void ShowAbout(AboutViewModel about) => AboutShown = about;
}
