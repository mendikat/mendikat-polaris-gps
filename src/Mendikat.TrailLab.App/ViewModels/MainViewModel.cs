using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using Mendikat.TrailLab.App.Resources;
using Mendikat.TrailLab.App.Services;
using Mendikat.TrailLab.App.Settings;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Domain.Model;
using Mendikat.TrailLab.Gpx;
using Mendikat.TrailLab.Mapping.Coordinates;
using Mendikat.TrailLab.Mapping.Providers;
using Microsoft.Extensions.Logging;

namespace Mendikat.TrailLab.App.ViewModels;

/// <summary>Main window state: open documents, File/View commands, map provider and status bar.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly IDocumentStore _store;
    private readonly ISettingsStore _settings;
    private readonly ILogger _logger;
    private DocumentViewModel? _activeDocument;
    private string _statusMessage = Strings.StatusReady;
    private string? _mapStatus;
    private string _pointerUtm = Strings.StatusPointerOutside;
    private string _pointerDms = Strings.StatusPointerOutside;
    private MapProvider _selectedMapProvider;
    private bool _isBusy;
    private int _nextColorIndex;
    private int _nextNewDocumentNumber = 1;

    public MainViewModel(IDialogService dialogs, IDocumentStore store, ISettingsStore settings, ILogger<MainViewModel> logger)
    {
        _dialogs = dialogs;
        _store = store;
        _settings = settings;
        _logger = logger;
        _selectedMapProvider = MapProviderCatalog.FindOrDefault(settings.Load().Map.Provider);

        NewGpxCommand = new RelayCommand(NewGpx, () => !IsBusy);
        OpenGpxCommand = new RelayCommand(_ => OpenAsync(DocumentFormat.Gpx), _ => !IsBusy);
        OpenWptCommand = new RelayCommand(_ => OpenAsync(DocumentFormat.Wpt), _ => !IsBusy);
        SaveCommand = new RelayCommand(_ => SaveActiveAsync(), _ => !IsBusy && ActiveDocument?.IsDirty == true);
        SaveAsCommand = new RelayCommand(_ => SaveActiveAsAsync(), _ => !IsBusy && ActiveDocument is not null);
        CloseDocumentCommand = new RelayCommand(
            p => CloseDocumentAsync(p as DocumentViewModel ?? ActiveDocument),
            p => !IsBusy && (p as DocumentViewModel ?? ActiveDocument) is not null);
        ExitCommand = new RelayCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty));
        ZoomInCommand = new RelayCommand(() => MapRequested?.Invoke(this, new MapRequest.ZoomIn()));
        ZoomOutCommand = new RelayCommand(() => MapRequested?.Invoke(this, new MapRequest.ZoomOut()));
        ZoomToDataCommand = new RelayCommand(ZoomToData, () => GetDataBounds() is not null);

        ElevationProfileCommand = new RelayCommand(OpenProfile, () => ActiveDocument?.SelectedLayer?.IsSequence == true);
        InitializeEditingCommands();
        InitializeToolCommands();
    }

    /// <summary>Asks the view to close the main window; the window then calls <see cref="ConfirmCloseAllAsync"/>.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>Asks the map view to navigate.</summary>
    public event EventHandler<MapRequest>? MapRequested;

    /// <summary>Asks the view to show the elevation profile window (PROF-01).</summary>
    public event EventHandler<ProfileViewModel>? ProfileRequested;

    public ObservableCollection<DocumentViewModel> Documents { get; } = [];

    public DocumentViewModel? ActiveDocument
    {
        get => _activeDocument;
        set
        {
            var previous = _activeDocument;
            if (SetProperty(ref _activeDocument, value))
            {
                if (previous is not null)
                {
                    previous.PropertyChanged -= OnActiveDocumentPropertyChanged;
                }

                if (value is not null)
                {
                    value.PropertyChanged += OnActiveDocumentPropertyChanged;
                }

                OnPropertyChanged(nameof(Title));
                RefreshCommands();
            }
        }
    }

    public bool HasDocuments => Documents.Count > 0;

    /// <summary>Window title (APP-01): active document, with "*" if it has unsaved changes.</summary>
    public string Title => ActiveDocument is null ? Strings.AppTitle : $"{ActiveDocument.Header} — {Strings.AppTitle}";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>Cartography problem shown in the status bar, or null when tiles load fine (MAP-07).</summary>
    public string? MapStatus
    {
        get => _mapStatus;
        private set => SetProperty(ref _mapStatus, value);
    }

    /// <summary>Pointer position in UTM/UPS (WGS84), e.g. "30T 555555 4750000" (COORD-01).</summary>
    public string PointerUtm
    {
        get => _pointerUtm;
        private set => SetProperty(ref _pointerUtm, value);
    }

    /// <summary>Pointer position in WGS84 degrees, minutes and seconds (COORD-02).</summary>
    public string PointerDms
    {
        get => _pointerDms;
        private set => SetProperty(ref _pointerDms, value);
    }

    public IReadOnlyList<MapProvider> MapProviders => MapProviderCatalog.All;

    /// <summary>Active cartography; remembered between sessions (MAP-04).</summary>
    public MapProvider SelectedMapProvider
    {
        get => _selectedMapProvider;
        set
        {
            if (value is null || !SetProperty(ref _selectedMapProvider, value))
            {
                return;
            }

            MapStatus = null;
            _logger.LogInformation("Map provider changed to {Provider}", value.Id);
            var current = _settings.Load();
            if (!_settings.Save(current with { Map = current.Map with { Provider = value.Id } }))
            {
                StatusMessage = Strings.StatusMapSaveSettingsFailed;
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommands();
            }
        }
    }

    public RelayCommand NewGpxCommand { get; }

    public RelayCommand OpenGpxCommand { get; }

    public RelayCommand OpenWptCommand { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand SaveAsCommand { get; }

    public RelayCommand CloseDocumentCommand { get; }

    public RelayCommand ExitCommand { get; }

    public RelayCommand ZoomInCommand { get; }

    public RelayCommand ZoomOutCommand { get; }

    public RelayCommand ZoomToDataCommand { get; }

    public RelayCommand ElevationProfileCommand { get; }

    /// <summary>Updates the status bar coordinates; null when the pointer leaves the map.</summary>
    public void UpdatePointer(GeoPosition? position)
    {
        if (position is { } p)
        {
            PointerUtm = CoordinateFormatter.FormatUtm(p);
            PointerDms = CoordinateFormatter.FormatDms(p);
        }
        else
        {
            PointerUtm = Strings.StatusPointerOutside;
            PointerDms = Strings.StatusPointerOutside;
        }
    }

    /// <summary>Called by the map view when tiles of the active provider fail or succeed (MAP-07).</summary>
    public void ReportMapTiles(bool failed) =>
        MapStatus = failed ? string.Format(Strings.StatusMapLoadFailed, SelectedMapProvider.DisplayName) : null;

    /// <summary>Union of the extents of all open documents, or null if they contain no points.</summary>
    public GeoBounds? GetDataBounds() =>
        Documents.Select(d => d.Document.Data.GetBounds())
            .Aggregate((GeoBounds?)null, (acc, b) => acc is { } a ? (b is { } bb ? a.Union(bb) : a) : b);

    /// <summary>
    /// File › New GPX (APP-08): opens an unsaved GPX document "Sin título N.gpx" with one empty track, selected so
    /// that Ctrl+click on the map adds points to it. It has no file until it is saved (Save asks for a path).
    /// </summary>
    public void NewGpx()
    {
        var track = new Track { Name = Strings.NewTrackName };
        track.Segments.Add(new TrackSegment());
        var data = new TrailData { FormatState = new GpxFormatState { SourceVersion = "1.1" } };
        data.Tracks.Add(track);

        var title = string.Format(Strings.NewDocumentTitle, _nextNewDocumentNumber++);
        var viewModel = new DocumentViewModel(TrailDocument.CreateNew(DocumentFormat.Gpx, title, data), _nextColorIndex++);
        Documents.Add(viewModel);
        OnPropertyChanged(nameof(HasDocuments));
        ActiveDocument = viewModel;
        StatusMessage = string.Format(Strings.StatusNewDocument, title);
        _logger.LogInformation("Created new GPX document {FileName}", title);
    }

    public async Task OpenAsync(DocumentFormat format)
    {
        var path = _dialogs.AskOpenFilePath(format);
        if (path is null)
        {
            return;
        }

        await OpenPathAsync(path, format);
    }

    /// <summary>Opens <paramref name="path"/> as a new document, or activates it if it is already open (APP-07).</summary>
    public async Task OpenPathAsync(string path, DocumentFormat format)
    {
        var fileName = Path.GetFileName(path);
        var existing = Documents.FirstOrDefault(d => d.Document.IsSameFile(path));
        if (existing is not null)
        {
            ActiveDocument = existing;
            StatusMessage = string.Format(Strings.StatusAlreadyOpen, fileName);
            return;
        }

        IsBusy = true;
        StatusMessage = string.Format(Strings.StatusOpening, fileName);
        try
        {
            var document = await _store.LoadAsync(path, format, CancellationToken.None);
            var viewModel = new DocumentViewModel(document, _nextColorIndex++);
            Documents.Add(viewModel);
            OnPropertyChanged(nameof(HasDocuments));
            ActiveDocument = viewModel;
            StatusMessage = string.Format(Strings.StatusOpened, fileName);
            _logger.LogInformation(
                "Opened {Format} document {FileName}: {Tracks} tracks, {Routes} routes, {Waypoints} waypoints, warnings: {Warnings}",
                format, fileName, document.Data.Tracks.Count, document.Data.Routes.Count, document.Data.Waypoints.Count,
                string.Join(",", document.Warnings.Select(w => w.Kind)));
            if (document.Data.GetBounds() is { } bounds)
            {
                MapRequested?.Invoke(this, new MapRequest.ZoomTo(bounds));
            }
        }
        catch (DocumentLoadException ex)
        {
            _logger.LogWarning(ex, "Could not open {FileName}: {Error}", fileName, ex.Error);
            StatusMessage = string.Format(Strings.StatusOpenFailed, fileName);
            _dialogs.ShowError(UserMessages.ForLoadError(ex, fileName));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Saves to the document's file; a new document has none, so it asks for a path as "Save as" (APP-08).</summary>
    /// <returns>True if the document was saved.</returns>
    public Task<bool> SaveActiveAsync() =>
        ActiveDocument is null ? Task.FromResult(false) : SaveToFileOrAskAsync(ActiveDocument);

    /// <returns>True if the document was saved.</returns>
    public Task<bool> SaveActiveAsAsync() =>
        ActiveDocument is null ? Task.FromResult(false) : SaveAsAsync(ActiveDocument);

    private Task<bool> SaveToFileOrAskAsync(DocumentViewModel document) =>
        document.FilePath is { } path ? SaveAsync(document, path) : SaveAsAsync(document);

    private async Task<bool> SaveAsAsync(DocumentViewModel document)
    {
        if (!EnsureCanSave(document))
        {
            return false;
        }

        var format = document.Document.Format;
        var path = _dialogs.AskSaveFilePath(format, document.FilePath ?? document.FileName);
        if (path is null)
        {
            return false;
        }

        // The format never changes silently (SAVE-01/02): a different extension gets the right one appended.
        var extension = format == DocumentFormat.Gpx ? ".gpx" : ".wpt";
        if (!string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase))
        {
            path += extension;
            if (File.Exists(path) && !_dialogs.Confirm(string.Format(Strings.ConfirmOverwrite, Path.GetFileName(path))))
            {
                return false;
            }
        }

        return await SaveAsync(document, path);
    }

    /// <summary>Closes a document, asking first if it has unsaved changes (APP-04).</summary>
    /// <returns>False if the user cancelled or saving failed; the document then stays open with its changes.</returns>
    public async Task<bool> CloseDocumentAsync(DocumentViewModel? document)
    {
        if (document is null)
        {
            return false;
        }

        if (!await ConfirmDiscardOrSaveAsync(document))
        {
            return false;
        }

        var index = Documents.IndexOf(document);
        Documents.Remove(document);
        OnPropertyChanged(nameof(HasDocuments));
        if (ReferenceEquals(ActiveDocument, document))
        {
            ActiveDocument = Documents.Count == 0 ? null : Documents[Math.Min(index, Documents.Count - 1)];
        }

        StatusMessage = string.Format(Strings.StatusClosed, document.FileName);
        _logger.LogInformation("Closed document {FileName}", document.FileName);
        RefreshCommands();
        return true;
    }

    /// <summary>Called before exiting: asks about every modified document.</summary>
    /// <returns>False if the user cancelled for any document; nothing is closed in that case.</returns>
    public async Task<bool> ConfirmCloseAllAsync()
    {
        foreach (var document in Documents.Where(d => d.IsDirty).ToList())
        {
            ActiveDocument = document;
            if (!await ConfirmDiscardOrSaveAsync(document))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Opens the elevation profile of the track or route selected in the layers panel (APP-06, PROF-01).</summary>
    private void OpenProfile()
    {
        if (ActiveDocument is { SelectedLayer: { IsSequence: true } layer } doc)
        {
            ProfileRequested?.Invoke(this, new ProfileViewModel(doc, layer));
        }
    }

    private void ZoomToData()
    {
        if (GetDataBounds() is { } bounds)
        {
            MapRequested?.Invoke(this, new MapRequest.ZoomTo(bounds));
        }
    }

    private async Task<bool> ConfirmDiscardOrSaveAsync(DocumentViewModel document)
    {
        if (!document.IsDirty)
        {
            return true;
        }

        return _dialogs.AskSaveChanges(document.FileName) switch
        {
            SaveChangesChoice.Discard => true,
            SaveChangesChoice.Save => await SaveToFileOrAskAsync(document),
            _ => false,
        };
    }

    private async Task<bool> SaveAsync(DocumentViewModel document, string path)
    {
        if (!EnsureCanSave(document))
        {
            return false;
        }

        var fileName = Path.GetFileName(path);
        if (!ConfirmSaveIssues(document, fileName))
        {
            return false;
        }

        IsBusy = true;
        StatusMessage = string.Format(Strings.StatusSaving, fileName);
        try
        {
            await _store.SaveAsync(document.Document, path, CancellationToken.None);
            document.Document.MarkSaved(path);
            StatusMessage = string.Format(Strings.StatusSaved, fileName);
            _logger.LogInformation("Saved {Format} document {FileName}", document.Document.Format, fileName);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Could not save {FileName}", fileName);
            var template = ex switch
            {
                SaveVerificationException => Strings.ErrorSaveVerification,
                UnauthorizedAccessException => Strings.ErrorSaveAccessDenied,
                _ => Strings.ErrorSaveFailed,
            };
            StatusMessage = string.Format(Strings.StatusSaveFailed, fileName);
            _dialogs.ShowError(string.Format(template, fileName));
            return false;
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
    }

    /// <summary>
    /// Shows what will differ in the saved file (GPX 1.0 → 1.1, characters the encoding cannot store…) and asks
    /// to continue (GPX-04, WPE-04). The GPX upgrade is asked only once per document.
    /// </summary>
    private bool ConfirmSaveIssues(DocumentViewModel document, string fileName)
    {
        var issues = _store.CheckSave(document.Document)
            .Where(i => !(i.Kind == SaveIssueKind.GpxVersionUpgrade && document.GpxUpgradeAccepted))
            .ToList();
        if (issues.Count == 0)
        {
            return true;
        }

        var message = string.Format(Strings.ConfirmSaveWithIssues, fileName, string.Join("\n", issues.Select(UserMessages.ForSaveIssue)));
        if (!_dialogs.Confirm(message))
        {
            return false;
        }

        document.GpxUpgradeAccepted |= issues.Any(i => i.Kind == SaveIssueKind.GpxVersionUpgrade);
        return true;
    }

    private bool EnsureCanSave(DocumentViewModel document)
    {
        if (_store.CanSave(document.Document.Format))
        {
            return true;
        }

        _dialogs.ShowInfo(string.Format(Strings.SaveNotAvailable, UserMessages.FormatName(document.Document.Format)));
        return false;
    }

    private void OnActiveDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(DocumentViewModel.Header) or nameof(DocumentViewModel.IsDirty):
                OnPropertyChanged(nameof(Title));
                RefreshCommands();
                break;
            case nameof(DocumentViewModel.CanUndo) or nameof(DocumentViewModel.CanRedo) or nameof(DocumentViewModel.Detail):
                RefreshEditingCommands();
                break;
            case nameof(DocumentViewModel.SelectedLayer):
                ElevationProfileCommand.RaiseCanExecuteChanged();
                RefreshToolCommands();
                break;
        }
    }

    private void RefreshCommands()
    {
        NewGpxCommand.RaiseCanExecuteChanged();
        OpenGpxCommand.RaiseCanExecuteChanged();
        OpenWptCommand.RaiseCanExecuteChanged();
        SaveCommand.RaiseCanExecuteChanged();
        SaveAsCommand.RaiseCanExecuteChanged();
        CloseDocumentCommand.RaiseCanExecuteChanged();
        ZoomToDataCommand.RaiseCanExecuteChanged();
        RefreshEditingCommands();
        ElevationProfileCommand.RaiseCanExecuteChanged();
        RefreshToolCommands();
    }
}
