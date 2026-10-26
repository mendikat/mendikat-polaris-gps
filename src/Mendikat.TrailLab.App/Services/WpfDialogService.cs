using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Mendikat.TrailLab.App.Resources;
using Mendikat.TrailLab.App.ViewModels;
using Mendikat.TrailLab.App.Views;
using Mendikat.TrailLab.Domain.Documents;
using Microsoft.Win32;

namespace Mendikat.TrailLab.App.Services;

public sealed class WpfDialogService : IDialogService
{
    public string? AskOpenFilePath(DocumentFormat format)
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.DialogOpenGpxTitle,
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
            DefaultExt = ".gpx",
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

    public bool ShowToolDialog(object viewModel)
    {
        Window dialog = viewModel switch
        {
            SimplifyViewModel simplify => new SimplifyWindow(simplify),
            TimesViewModel times => new TimesWindow(times),
            DemViewModel dem => new DemWindow(dem),
            _ => throw new ArgumentException($"No dialog for {viewModel.GetType().Name}.", nameof(viewModel)),
        };
        dialog.Owner = Owner;
        return dialog.ShowDialog() == true;
    }

    public bool Confirm(string message) =>
        MessageBox.Show(Owner, message, Strings.DialogConfirmTitle, MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel)
        == MessageBoxResult.OK;

    public void ShowError(string message) =>
        MessageBox.Show(Owner, message, Strings.DialogErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowInfo(string message) =>
        MessageBox.Show(Owner, message, Strings.DialogInfoTitle, MessageBoxButton.OK, MessageBoxImage.Information);

    public void OpenExternal(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
        }
        catch (Win32Exception)
        {
            // No program is associated (e.g. no browser or mail client): tell the user where to go instead.
            ShowError(string.Format(Strings.ErrorOpenExternal, target));
        }
    }

    public void ShowAbout(AboutViewModel about) => new AboutWindow(about, OpenExternal) { Owner = Owner }.ShowDialog();

    private static Window Owner => Application.Current.MainWindow;

    private static string FilterFor(DocumentFormat format) => format switch
    {
        DocumentFormat.Gpx => Strings.FilterGpx,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };
}
