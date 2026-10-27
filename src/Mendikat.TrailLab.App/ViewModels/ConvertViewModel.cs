using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using Mendikat.TrailLab.App.Resources;
using Mendikat.TrailLab.App.Services;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Gpx;

namespace Mendikat.TrailLab.App.ViewModels;

/// <summary>What to do when the GPX of a converted file already exists (CONV-06).</summary>
public enum ExistingFileAction
{
    Rename,
    Overwrite,
    Skip,
}

/// <summary>An <see cref="ExistingFileAction"/> with its display name.</summary>
public sealed record ExistingFileOption(ExistingFileAction Action, string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>
/// "Convert to GPX" dialog (CONV-06): converts KML, KMZ and TCX files to GPX files, one per source, in the source
/// folder or another one. Runs in the background with progress and can be stopped between files; a file with an error
/// does not stop the others. Every GPX is verified and written safely, so none is ever left half-written.
/// </summary>
public sealed class ConvertViewModel : ObservableObject
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private readonly IDialogService _dialogs;
    private string? _selectedFile;
    private bool _useSourceFolder = true;
    private string? _targetFolder;
    private ExistingFileOption _existing;
    private bool _isConverting;
    private double _progress;
    private string _report = string.Empty;
    private CancellationTokenSource? _cancellation;

    public ConvertViewModel(IDialogService dialogs, IEnumerable<string> files)
    {
        _dialogs = dialogs;
        Files = new ObservableCollection<string>(files.Distinct(StringComparer.OrdinalIgnoreCase));
        ExistingOptions =
        [
            new(ExistingFileAction.Rename, Strings.ConvertExistingRename),
            new(ExistingFileAction.Overwrite, Strings.ConvertExistingOverwrite),
            new(ExistingFileAction.Skip, Strings.ConvertExistingSkip),
        ];
        _existing = ExistingOptions[0];
        AddFilesCommand = new RelayCommand(AddFiles, () => !IsConverting);
        RemoveFileCommand = new RelayCommand(RemoveFile, () => !IsConverting && SelectedFile is not null);
        BrowseCommand = new RelayCommand(Browse, () => !IsConverting);
        ConvertCommand = new RelayCommand(_ => ConvertAsync(), _ => CanConvert);
        StopCommand = new RelayCommand(Stop, () => IsConverting);
        Files.CollectionChanged += (_, _) => RefreshCommands();
    }

    public ObservableCollection<string> Files { get; }

    public string? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (SetProperty(ref _selectedFile, value))
            {
                RemoveFileCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>True: each GPX goes next to its source. False: all go to <see cref="TargetFolder"/>.</summary>
    public bool UseSourceFolder
    {
        get => _useSourceFolder;
        set
        {
            if (SetProperty(ref _useSourceFolder, value))
            {
                OnPropertyChanged(nameof(UseOtherFolder));
                RefreshCommands();
            }
        }
    }

    public bool UseOtherFolder
    {
        get => !UseSourceFolder;
        set => UseSourceFolder = !value;
    }

    public string? TargetFolder
    {
        get => _targetFolder;
        set
        {
            if (SetProperty(ref _targetFolder, value))
            {
                RefreshCommands();
            }
        }
    }

    public IReadOnlyList<ExistingFileOption> ExistingOptions { get; }

    public ExistingFileOption Existing
    {
        get => _existing;
        set => SetProperty(ref _existing, value ?? ExistingOptions[0]);
    }

    public RelayCommand AddFilesCommand { get; }

    public RelayCommand RemoveFileCommand { get; }

    public RelayCommand BrowseCommand { get; }

    public RelayCommand ConvertCommand { get; }

    public RelayCommand StopCommand { get; }

    public bool IsConverting
    {
        get => _isConverting;
        private set
        {
            if (SetProperty(ref _isConverting, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                RefreshCommands();
            }
        }
    }

    public bool IsIdle => !IsConverting;

    /// <summary>Files done, 0 to 1.</summary>
    public double Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
    }

    /// <summary>One line per file (converted, skipped or failed, with its warnings) and a summary.</summary>
    public string Report
    {
        get => _report;
        private set => SetProperty(ref _report, value);
    }

    public int Converted { get; private set; }

    public int Skipped { get; private set; }

    public int Failed { get; private set; }

    public bool CanConvert => !IsConverting && Files.Count > 0 && (UseSourceFolder || !string.IsNullOrWhiteSpace(TargetFolder));

    /// <summary>Converts every file in order. The report is rebuilt each time.</summary>
    public async Task ConvertAsync()
    {
        if (!CanConvert)
        {
            if (!UseSourceFolder && string.IsNullOrWhiteSpace(TargetFolder))
            {
                Report = Strings.ConvertNoFolder;
            }

            return;
        }

        _cancellation = new CancellationTokenSource();
        var token = _cancellation.Token;
        IsConverting = true;
        Converted = Skipped = Failed = 0;
        Progress = 0;
        var lines = new List<string>();
        Report = string.Empty;
        var files = Files.ToList();
        var folder = UseSourceFolder ? null : TargetFolder;
        var existing = Existing.Action;
        try
        {
            for (var i = 0; i < files.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var source = files[i];
                lines.AddRange(await Task.Run(() => ConvertOne(source, folder, existing, token), token));
                Report = string.Join(Environment.NewLine, lines);
                Progress = (double)(i + 1) / files.Count;
            }

            lines.Add(Summary());
        }
        catch (OperationCanceledException)
        {
            lines.Add(Strings.ConvertCancelled);
            lines.Add(Summary());
        }
        finally
        {
            Report = string.Join(Environment.NewLine, lines);
            IsConverting = false;
            _cancellation.Dispose();
            _cancellation = null;
        }
    }

    /// <summary>Stops after the file being converted (also called when the dialog is closed).</summary>
    public void Stop() => _cancellation?.Cancel();

    public string StatusSummary => string.Format(Spanish, Strings.StatusConvertedFiles, Converted, Skipped, Failed);

    private string Summary() => string.Format(Spanish, Strings.ConvertSummary, Converted, Skipped, Failed);

    private List<string> ConvertOne(string source, string? folder, ExistingFileAction existing, CancellationToken token)
    {
        var name = Path.GetFileName(source);
        SourceFileResult read;
        try
        {
            read = TrailFileReader.ReadFile(source, null, token);
        }
        catch (DocumentLoadException ex)
        {
            Failed++;
            return [string.Format(Strings.ConvertReportError, UserMessages.ForLoadError(ex, name))];
        }

        if (read.Format == SourceFormat.Gpx)
        {
            Skipped++;
            return [string.Format(Strings.ConvertReportAlreadyGpx, name)];
        }

        var target = Path.Combine(folder ?? Path.GetDirectoryName(Path.GetFullPath(source))!, Path.ChangeExtension(name, ".gpx"));
        if (File.Exists(target))
        {
            switch (existing)
            {
                case ExistingFileAction.Skip:
                    Skipped++;
                    return [string.Format(Strings.ConvertReportSkipped, name, Path.GetFileName(target))];
                case ExistingFileAction.Rename:
                    target = GpxConversion.FreePath(target);
                    break;
            }
        }

        token.ThrowIfCancellationRequested();
        try
        {
            GpxConversion.Write(read.Result.Data, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Failed++;
            return [string.Format(Strings.ConvertReportWriteError, name, Path.GetFileName(target))];
        }

        Converted++;
        var data = read.Result.Data;
        var lines = new List<string>
        {
            string.Format(
                Spanish,
                Strings.ConvertReportOk,
                name,
                Path.GetFileName(target),
                data.Tracks.Count,
                data.Tracks.Sum(t => t.PointCount).ToString("N0", Spanish),
                data.Waypoints.Count),
        };
        lines.AddRange(read.Result.Warnings
            .Where(w => w.Kind != LoadWarningKind.ConvertedFrom)
            .Select(w => string.Format(Strings.ConvertReportWarning, UserMessages.ForWarning(w))));
        return lines;
    }

    private void AddFiles()
    {
        foreach (var file in _dialogs.AskConvertFilePaths() ?? [])
        {
            if (!Files.Contains(file, StringComparer.OrdinalIgnoreCase))
            {
                Files.Add(file);
            }
        }
    }

    private void RemoveFile()
    {
        if (SelectedFile is { } file)
        {
            Files.Remove(file);
        }
    }

    private void Browse()
    {
        var initial = TargetFolder ?? (Files.Count > 0 ? Path.GetDirectoryName(Files[0]) : null);
        if (_dialogs.AskFolder(initial) is { } folder)
        {
            TargetFolder = folder;
            UseSourceFolder = false;
        }
    }

    private void RefreshCommands()
    {
        OnPropertyChanged(nameof(CanConvert));
        AddFilesCommand.RaiseCanExecuteChanged();
        RemoveFileCommand.RaiseCanExecuteChanged();
        BrowseCommand.RaiseCanExecuteChanged();
        ConvertCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
    }
}
