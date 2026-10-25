using Mendikat.TrailLab.App.Services;
using Mendikat.TrailLab.Domain.Documents;

namespace Mendikat.TrailLab.Tests.Fakes;

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

    public string? AskSaveFilePath(DocumentFormat format, string suggestedPath) => SavePath;

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
}
