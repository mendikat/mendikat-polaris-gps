using System.Collections.ObjectModel;
using System.IO;
using Mendikat.TrailLab.App.Resources;
using Mendikat.TrailLab.App.Services;
using Mendikat.TrailLab.Mapping.Local;
using Microsoft.Extensions.Logging;

namespace Mendikat.TrailLab.App.ViewModels;

/// <summary>
/// Local cartography (increment 9, D7): File › Open cartography loads OziExplorer .map and CompeGPS .imp maps, drawn
/// under the tracks, and .asc/.hgt DEMs, used for elevations. They are not documents: they are not saved and are not
/// remembered between sessions (D10).
/// </summary>
public sealed partial class MainViewModel
{
    public ObservableCollection<LocalMapItemViewModel> LocalMaps { get; } = [];

    public bool HasLocalMaps => LocalMaps.Count > 0;

    /// <summary>"Mapas locales (2)".</summary>
    public string LocalMapsHeader => string.Format(Strings.LocalMapsHeader, LocalMaps.Count);

    /// <summary>Reads a local file off the UI thread. Set at startup (Windows image decoding); tests may replace it.</summary>
    public Func<string, CancellationToken, LocalMapContent>? LocalMapLoader { get; init; }

    public RelayCommand OpenLocalMapCommand { get; private set; } = null!;

    public RelayCommand RemoveLocalMapCommand { get; private set; } = null!;

    public RelayCommand ZoomToLocalMapCommand { get; private set; } = null!;

    /// <summary>A map was added: the view adds its layer.</summary>
    public event EventHandler<LocalMapItemViewModel>? LocalMapAdded;

    /// <summary>A map was removed: the view removes its layer.</summary>
    public event EventHandler<LocalMapItemViewModel>? LocalMapRemoved;

    /// <summary>The DEMs in use, in the order they were opened (status bar and "Adjust elevations").</summary>
    public IReadOnlyList<ElevationGrid> LocalDems => LocalMaps.Where(m => m.IsDem && m.IsVisible).Select(m => m.Dem!).ToList();

    /// <summary>Asks for files and opens them one after another.</summary>
    public async Task OpenLocalMapsAsync()
    {
        foreach (var path in _dialogs.AskLocalMapPaths() ?? [])
        {
            await OpenLocalMapAsync(path);
        }
    }

    /// <summary>Loads a map or DEM (LMAP-02): on failure a message explains why and nothing is added.</summary>
    public async Task OpenLocalMapAsync(string path)
    {
        var fileName = Path.GetFileName(path);
        if (LocalMaps.Any(m => string.Equals(Path.GetFullPath(m.Content.Path), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = string.Format(Strings.StatusAlreadyOpen, fileName);
            return;
        }

        var loader = LocalMapLoader ?? throw new InvalidOperationException("No local map loader configured.");
        IsBusy = true;
        StatusMessage = string.Format(Strings.StatusOpening, fileName);
        using var cancellation = new CancellationTokenSource();
        SetOpenCancellation(cancellation);
        try
        {
            var content = await Task.Run(() => loader(path, cancellation.Token), cancellation.Token);
            var item = new LocalMapItemViewModel(content);
            item.VisibilityChanged += (_, _) => OnPropertyChanged(nameof(LocalDems));
            LocalMaps.Insert(0, item); // the newest on top (LMAP-11)
            OnLocalMapsChanged();
            LocalMapAdded?.Invoke(this, item);
            MapRequested?.Invoke(this, new MapRequest.ZoomTo(item.Bounds));
            StatusMessage = string.Format(item.IsDem ? Strings.StatusLocalDemOpened : Strings.StatusLocalMapOpened, fileName);
            _logger.LogInformation("Opened local {Kind} {FileName}: {Details}", item.IsDem ? "DEM" : "map", fileName, item.Details);
        }
        catch (LocalMapException ex)
        {
            _logger.LogWarning(ex, "Could not open local map {FileName}: {Error}", fileName, ex.Error);
            StatusMessage = string.Format(Strings.StatusOpenFailed, fileName);
            _dialogs.ShowError(UserMessages.ForLocalMapError(ex, fileName));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            StatusMessage = string.Format(Strings.StatusOpenCancelled, fileName);
        }
        finally
        {
            SetOpenCancellation(null);
            IsBusy = false;
        }
    }

    /// <summary>The drawing order changed: the first item of <see cref="LocalMaps"/> is drawn on top (LMAP-11).</summary>
    public event EventHandler? LocalMapsReordered;

    /// <summary>
    /// Moves a cartography in the list (drag and drop, or Ctrl+↑/↓). The list order is the drawing order: first on top,
    /// last just above the online cartography, which always stays at the bottom.
    /// </summary>
    public void MoveLocalMap(int from, int to)
    {
        if (from < 0 || from >= LocalMaps.Count || to < 0 || to >= LocalMaps.Count || from == to)
        {
            return;
        }

        LocalMaps.Move(from, to);
        LocalMapsReordered?.Invoke(this, EventArgs.Empty);
        _logger.LogInformation("Moved local map {FileName} from {From} to {To}", LocalMaps[to].Name, from, to);
    }

    public void RemoveLocalMap(LocalMapItemViewModel? item)
    {
        if (item is null || !LocalMaps.Remove(item))
        {
            return;
        }

        OnLocalMapsChanged();
        LocalMapRemoved?.Invoke(this, item);
        StatusMessage = string.Format(Strings.StatusLocalMapRemoved, item.Name);
        _logger.LogInformation("Removed local map {FileName}", item.Name);
    }

    private void InitializeLocalMapCommands()
    {
        OpenLocalMapCommand = new RelayCommand(_ => OpenLocalMapsAsync(), _ => !IsBusy && LocalMapLoader is not null);
        RemoveLocalMapCommand = new RelayCommand(
            p =>
            {
                RemoveLocalMap(p as LocalMapItemViewModel);
                return Task.CompletedTask;
            },
            p => p is LocalMapItemViewModel);
        ZoomToLocalMapCommand = new RelayCommand(
            p =>
            {
                if (p is LocalMapItemViewModel item)
                {
                    MapRequested?.Invoke(this, new MapRequest.ZoomTo(item.Bounds));
                }

                return Task.CompletedTask;
            },
            p => p is LocalMapItemViewModel);
    }

    private void RefreshLocalMapCommands() => OpenLocalMapCommand?.RaiseCanExecuteChanged();

    private void OnLocalMapsChanged()
    {
        OnPropertyChanged(nameof(HasLocalMaps));
        OnPropertyChanged(nameof(LocalMapsHeader));
        OnPropertyChanged(nameof(LocalDems));
    }
}
