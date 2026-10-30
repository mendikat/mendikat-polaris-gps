using System.Net.Http;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Tiling.Layers;
using Polaris.Gps.App.ViewModels;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Mapping.Coordinates;
using Mapsui.Extensions;
using Polaris.Gps.Mapping.Layers;
using Polaris.Gps.Mapping.Local;
using Polaris.Gps.Mapping.Providers;
using Polaris.Gps.Mapping.Tiles;
using Microsoft.Extensions.Logging;

namespace Polaris.Gps.App.Map;

/// <summary>
/// Keeps the shared Mapsui map in sync with the open documents and the selected provider (ADR-0002):
/// one base tile layer, the layers of every document item, and a selection highlight on top.
/// Opening a document never removes others (MAP-09).
/// </summary>
public sealed class MapController : IDisposable
{
    /// <summary>Initial view when nothing is open: mainland Spain and the Balearic Islands.</summary>
    private static readonly GeoBounds InitialBounds = new(35.9, -9.5, 43.9, 4.4);

    /// <summary>Minimum size of a fitted area (meters in Web Mercator), so a single point is not zoomed to the maximum.</summary>
    private const double MinimumFitSize = 1000;

    private const int BaseLayerGroup = 0;
    private const int LocalMapLayerGroup = 1;
    private const int GridLayerGroup = 2;
    private const int DataLayerGroup = 3;
    private const int SelectionLayerGroup = 4;
    private const int HoverLayerGroup = 5;

    private readonly Mapsui.Map _map;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly Dictionary<DocumentViewModel, IReadOnlyList<ItemLayers>> _documentLayers = [];
    private readonly MemoryLayer _selectionLayer = new("selection") { Style = null };
    private readonly MemoryLayer _hoverLayer = new("profile-hover") { Style = null };
    private readonly MemoryLayer _gridLayer = new("utm-grid") { Style = null, Enabled = false };
    private readonly Dictionary<LocalMapItemViewModel, TileLayer> _localLayers = [];
    private TileLayer? _baseLayer;
    private ObservedTileSource? _tileSource;
    private bool _lastTileFailed;
    private TrackColorMode _trackColoring;

    public MapController(Mapsui.Map map, HttpClient httpClient, ILogger<MapController> logger)
    {
        _map = map;
        _httpClient = httpClient;
        _logger = logger;
        _map.Layers.Add(_selectionLayer, SelectionLayerGroup);
        _map.Layers.Add(_hoverLayer, HoverLayerGroup);
        _map.Layers.Add(_gridLayer, GridLayerGroup);
        ZoomTo(InitialBounds);
    }

    /// <summary>Raised (on a background thread) when tile loading switches between failing and working.</summary>
    public event EventHandler<bool>? TileStatusChanged;

    /// <summary>Current resolution in Web Mercator meters per pixel.</summary>
    public double Resolution => _map.Navigator.Viewport.Resolution;

    public void SetProvider(MapProvider provider)
    {
        if (_tileSource is not null)
        {
            _tileSource.TileLoaded -= OnTileLoaded;
            _tileSource.TileFailed -= OnTileFailed;
        }

        if (_baseLayer is not null)
        {
            _map.Layers.Clear(BaseLayerGroup);
            _baseLayer.Dispose();
        }

        _lastTileFailed = false;
        _tileSource = new ObservedTileSource(provider);
        _tileSource.TileLoaded += OnTileLoaded;
        _tileSource.TileFailed += OnTileFailed;
        _baseLayer = new TileLayer(_tileSource, httpClient: _httpClient) { Name = $"base:{provider.Id}" };
        _map.Layers.Add(_baseLayer, BaseLayerGroup);
        _logger.LogInformation("Base map set to {Provider}", provider.Id);
    }

    /// <summary>Adds or rebuilds the layers of a document (after opening it or after an edit, EDIT-07).</summary>
    public void ShowDocument(DocumentViewModel document)
    {
        var old = _documentLayers.GetValueOrDefault(document) ?? [];
        var items = TrailLayerFactory.CreateLayers(document.Document.Data, MapStyles.ColorFor(document.ColorIndex), document.FileName, _trackColoring);
        foreach (var item in items)
        {
            var visible = document.Layers.FirstOrDefault(l => ReferenceEquals(l.Model, item.Model))?.IsVisible ?? true;
            foreach (var layer in item.Layers)
            {
                layer.Enabled = visible;
                layer.Tag = document;
            }
        }

        _documentLayers[document] = items;
        RemoveLayers(old.SelectMany(i => i.Layers).ToList());
        var newLayers = items.SelectMany(i => i.Layers).ToList();
        if (newLayers.Count > 0)
        {
            _map.Layers.Add(newLayers, DataLayerGroup);
        }
    }

    public void RemoveDocument(DocumentViewModel document)
    {
        if (_documentLayers.Remove(document, out var items))
        {
            RemoveLayers(items.SelectMany(i => i.Layers).ToList());
        }
    }

    /// <summary>Shows or hides one item on the map (LAY-01).</summary>
    public void SetVisibility(DocumentViewModel document, LayerItemViewModel item)
    {
        var layers = _documentLayers.GetValueOrDefault(document)?.FirstOrDefault(i => ReferenceEquals(i.Model, item.Model))?.Layers ?? [];
        foreach (var layer in layers)
        {
            layer.Enabled = item.IsVisible;
        }

        _map.RefreshGraphics();
    }

    /// <summary>Highlights the given positions (selected points, or a point being dragged).</summary>
    public void ShowSelection(IEnumerable<GeoPosition> positions)
    {
        _selectionLayer.Features = TrailLayerFactory.SelectionFeatures(positions).ToList();
        _selectionLayer.DataHasChanged();
    }

    /// <summary>Marks the point under the pointer in the elevation profile, or clears the mark (PROF-04).</summary>
    public void ShowHover(GeoPosition? position)
    {
        _hoverLayer.Features = position is { } p ? TrailLayerFactory.Markers([p], MapStyles.HoverPoint).ToList() : [];
        _hoverLayer.DataHasChanged();
    }

    /// <summary>The visible point nearest to <paramref name="world"/>, with its document (LAY-03).</summary>
    public (DocumentViewModel Document, TrailPoint Point)? HitTest(MPoint world)
    {
        var layers = _documentLayers.Values.SelectMany(items => items.SelectMany(i => i.Layers));
        return PointHitTester.FindNearest(layers, world, Resolution) is { } hit
            ? ((DocumentViewModel)hit.Layer.Tag!, hit.Point)
            : null;
    }

    /// <summary>Centers the map on <paramref name="position"/> if it is outside the visible area.</summary>
    public void EnsureVisible(GeoPosition position)
    {
        var viewport = _map.Navigator.Viewport;
        var (x, y) = WebMercator.ToWorld(position);
        var halfWidth = viewport.Width * viewport.Resolution / 2 * 0.9;
        var halfHeight = viewport.Height * viewport.Resolution / 2 * 0.9;
        if (Math.Abs(x - viewport.CenterX) > halfWidth || Math.Abs(y - viewport.CenterY) > halfHeight)
        {
            _map.Navigator.CenterOn(x, y);
        }
    }

    /// <summary>Locks map panning while a point is dragged (EDIT-04).</summary>
    public void LockPan(bool locked) => _map.Navigator.PanLock = locked;

    public void Handle(MapRequest request)
    {
        switch (request)
        {
            case MapRequest.ZoomIn:
                _map.Navigator.ZoomIn();
                break;
            case MapRequest.ZoomOut:
                _map.Navigator.ZoomOut();
                break;
            case MapRequest.ZoomTo zoom:
                ZoomTo(zoom.Bounds);
                break;
        }
    }

    /// <summary>Draws a local map above the base cartography and below the tracks (LMAP-01). DEMs are not drawn.</summary>
    public void AddLocalMap(LocalMapItemViewModel item)
    {
        if (item.Content is not ILocalMap map || _localLayers.ContainsKey(item))
        {
            return;
        }

        var layer = new TileLayer(map.CreateTileSource(item.Name)) { Name = $"local:{item.Name}", Enabled = item.IsVisible };
        _localLayers[item] = layer;
        _map.Layers.Add(layer, LocalMapLayerGroup);
    }

    public void RemoveLocalMap(LocalMapItemViewModel item)
    {
        if (_localLayers.Remove(item, out var layer))
        {
            RemoveLayers([layer]);
        }
    }

    public void SetLocalMapOpacity(LocalMapItemViewModel item)
    {
        if (_localLayers.TryGetValue(item, out var layer))
        {
            layer.Opacity = item.Opacity;
            _map.RefreshGraphics();
        }
    }

    /// <summary>
    /// Redraws the user cartographies in the given order, first on top (LMAP-11). They stay in their own layer group,
    /// so the online cartography is always below them and the tracks always above.
    /// </summary>
    public void OrderLocalMaps(IReadOnlyList<LocalMapItemViewModel> topFirst)
    {
        var layers = topFirst.Select(i => _localLayers.GetValueOrDefault(i)).OfType<TileLayer>().ToList();
        if (layers.Count == 0)
        {
            return;
        }

        foreach (var layer in layers)
        {
            _map.Layers.Remove(layer);
        }

        // Within a group, a layer added later is drawn on top: add from the bottom one up.
        for (var i = layers.Count - 1; i >= 0; i--)
        {
            _map.Layers.Add(layers[i], LocalMapLayerGroup);
        }

        _map.RefreshGraphics();
    }

    public void SetLocalMapVisibility(LocalMapItemViewModel item)
    {
        if (_localLayers.TryGetValue(item, out var layer))
        {
            layer.Enabled = item.IsVisible;
            _map.RefreshData();
        }
    }

    public void Dispose()
    {
        foreach (var layer in _localLayers.Values)
        {
            layer.Dispose();
        }

        foreach (var layer in _documentLayers.Values.SelectMany(items => items.SelectMany(i => i.Layers)))
        {
            layer.Dispose();
        }

        _selectionLayer.Dispose();
        _hoverLayer.Dispose();
        _gridLayer.Dispose();
        _baseLayer?.Dispose();
    }

    /// <summary>
    /// Draws the UTM grid of the visible area in <paramref name="datum"/>, or hides it. Called when the view changes
    /// and when the setting changes; cheap (a few dozen sampled lines).
    /// </summary>
    public void UpdateGrid(bool show, CoordinateDatum datum)
    {
        _gridLayer.Enabled = show;
        if (!show)
        {
            _gridLayer.Features = [];
            _gridLayer.DataHasChanged();
            return;
        }

        var viewport = _map.Navigator.Viewport;
        var extent = viewport.ToExtent();
        if (extent is null || viewport.Width <= 0)
        {
            return;
        }

        var sw = WebMercator.ToGeo(extent.MinX, extent.MinY);
        var ne = WebMercator.ToGeo(extent.MaxX, extent.MaxY);
        var view = new GeoBounds(sw.Latitude, sw.Longitude, ne.Latitude, ne.Longitude);
        var centreLatitude = (sw.Latitude + ne.Latitude) / 2 * Math.PI / 180;
        var lines = UtmGrid.Lines(view, viewport.Resolution * Math.Cos(centreLatitude), datum);
        _gridLayer.Features = TrailLayerFactory.GridFeatures(lines, view).ToList();
        _gridLayer.DataHasChanged();
    }

    /// <summary>Colours the tracks of every document by elevation, slope or speed, or with their document colour (None).</summary>
    public void SetTrackColoring(TrackColorMode mode, IEnumerable<DocumentViewModel> documents)
    {
        if (mode == _trackColoring)
        {
            return;
        }

        _trackColoring = mode;
        foreach (var document in documents)
        {
            ShowDocument(document);
        }
    }

    /// <summary>Removes and disposes layers. Mapsui rejects empty lists, so nothing is called for an empty one.</summary>
    private void RemoveLayers(List<ILayer> layers)
    {
        if (layers.Count == 0)
        {
            return;
        }

        foreach (var layer in layers)
        {
            _map.Layers.Remove(layer);
            layer.Dispose();
        }
    }

    private void ZoomTo(GeoBounds bounds)
    {
        var extent = TrailLayerFactory.WorldExtent(bounds)!;
        var grow = Math.Max(MinimumFitSize, Math.Max(extent.Width, extent.Height) * 0.1);
        _map.Navigator.ZoomToBox(extent.Grow(grow), MBoxFit.Fit);
    }

    private void OnTileLoaded(object? sender, EventArgs e) => UpdateTileStatus(failed: false, error: null);

    private void OnTileFailed(object? sender, Exception error) => UpdateTileStatus(failed: true, error);

    private void UpdateTileStatus(bool failed, Exception? error)
    {
        if (failed == _lastTileFailed)
        {
            return;
        }

        _lastTileFailed = failed;
        if (failed)
        {
            // Log only state changes: when offline, every tile fails and would flood the log.
            _logger.LogWarning("Tiles of {Provider} are failing: {Error}", _tileSource?.Provider.Id, error?.GetType().Name + ": " + error?.Message);
        }
        else
        {
            _logger.LogInformation("Tiles of {Provider} are loading again", _tileSource?.Provider.Id);
        }

        TileStatusChanged?.Invoke(this, failed);
    }
}
