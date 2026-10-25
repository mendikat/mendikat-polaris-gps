using System.IO;
using System.Windows;
using Mendikat.TrailLab.App.Resources;
using Mendikat.TrailLab.Domain.Documents;
using Microsoft.Win32;

namespace Mendikat.TrailLab.App.Services;

public sealed class WpfDialogService : IDialogService
{
    public string? AskOpenFilePath(DocumentFormat format)
    {
        var dialog = new OpenFileDialog
        {
            Title = format == DocumentFormat.Gpx ? Strings.DialogOpenGpxTitle : Strings.DialogOpenWptTitle,
            Filter = FilterFor(format),
            CheckFileExists = true,
            Multiselect = false,
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? AskSaveFilePath(DocumentFormat format, string suggestedPath)
    {
        var dialog = new SaveFileDialog
        {
            Title = Strings.DialogSaveAsTitle,
            Filter = FilterFor(format),
            FileName = Path.GetFileName(suggestedPath),
            InitialDirectory = Path.GetDirectoryName(suggestedPath),
            DefaultExt = format == DocumentFormat.Gpx ? ".gpx" : ".wpt",
            AddExtension = true,
            OverwritePrompt = true,
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public SaveChangesChoice AskSaveChanges(string documentName)
    {
        var result = MessageBox.Show(
            Owner,
            string.Format(Strings.SaveChangesQuestion, documentName),
            Strings.AppTitle,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel);
        return result switch
        {
            MessageBoxResult.Yes => SaveChangesChoice.Save,
            MessageBoxResult.No => SaveChangesChoice.Discard,
            _ => SaveChangesChoice.Cancel,
        };
    }

    public bool Confirm(string message) =>
        MessageBox.Show(Owner, message, Strings.DialogConfirmTitle, MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel)
        == MessageBoxResult.OK;

    public void ShowError(string message) =>
        MessageBox.Show(Owner, message, Strings.DialogErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowInfo(string message) =>
        MessageBox.Show(Owner, message, Strings.DialogInfoTitle, MessageBoxButton.OK, MessageBoxImage.Information);

    private static Window Owner => Application.Current.MainWindow;

    private static string FilterFor(DocumentFormat format) =>
        format == DocumentFormat.Gpx ? Strings.FilterGpx : Strings.FilterWpt;
}
