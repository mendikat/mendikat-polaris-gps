using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using Mapsui;
using Mapsui.Extensions;
using Polaris.Gps.App.Map;
using Polaris.Gps.App.ViewModels;
using Polaris.Gps.App.Views;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Mapping.Coordinates;

namespace Polaris.Gps.App;

/// <summary>
/// Main window. Code-behind only wires the Mapsui control (a view concern) to the view model:
/// layers follow the documents, map requests are executed, pointer coordinates are reported, and
/// map clicks / drags / right clicks select or move points (LAY-03, EDIT-04).
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>A press must move at least this many pixels to become a drag; smaller movements never move a point.</summary>
    private const double MinimumDragPixels = 4;

    /// <summary>Widest scale bar; the round distance chosen makes it between 40 % and 100 % of this.</summary>
    private const double MaxScaleBarPixels = 220;

    private readonly MainViewModel _viewModel;
    private readonly MapController _map;
    private bool _closeConfirmed;
    private DragState? _drag;
    private System.Windows.Point? _addPointPress;
    private System.Windows.Point? _drawPress;
    private GeoPosition? _contextPosition;
    private bool _contextOnPoint;
    private readonly List<ProfileWindow> _profileWindows = [];

    public MainWindow(MainViewModel viewModel, Func<Mapsui.Map, MapController> createMapController)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        MapView.Map = new Mapsui.Map();
        _map = createMapController(MapView.Map);
        _map.SetProvider(viewModel.SelectedMapProvider);
        _map.SetTrackColoring(viewModel.TrackColoring, []);
        _map.TileStatusChanged += (_, failed) => Dispatcher.BeginInvoke(() => _viewModel.ReportMapTiles(failed));
        viewModel.PointAt = position =>
        {
            var (x, y) = WebMercator.ToWorld(position);
            return _map.HitTest(new MPoint(x, y))?.Point;
        };

        viewModel.ExitRequested += (_, _) => Close();
        viewModel.MapRequested += (_, request) => _map.Handle(request);
        viewModel.ProfileRequested += (_, profile) => ShowProfile(profile);
        viewModel.Documents.CollectionChanged += OnDocumentsChanged;
        viewModel.LocalMapAdded += (_, item) =>
        {
            _map.AddLocalMap(item);
            item.VisibilityChanged += (_, _) => _map.SetLocalMapVisibility(item);
            item.OpacityChanged += (_, _) => _map.SetLocalMapOpacity(item);
        };
        viewModel.LocalMapsReordered += (_, _) => _map.OrderLocalMaps(viewModel.LocalMaps);
        viewModel.LocalMapRemoved += (_, item) => _map.RemoveLocalMap(item);

        // Files dropped anywhere on the window are opened: tracks as documents, maps and DEMs as cartography (APP-10).
        AllowDrop = true;
        PreviewDragOver += OnFileDragOver;
        PreviewDrop += OnFileDrop;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        MapView.Map.Navigator.ViewportChanged += (_, _) => OnViewportChanged();
        _map.UpdateGrid(viewModel.ShowUtmGrid, viewModel.CoordinateDatum);
        OnViewportChanged();
        MapView.MapPointerMoved += OnMapPointerMoved;
        MapView.MapPointerPressed += OnMapPointerPressed;
        MapView.MapPointerReleased += OnMapPointerReleased;
        MapView.MapTapped += OnMapTapped;
        MapView.PreviewMouseRightButtonDown += OnMapRightButtonDown;
        MapView.PreviewMouseLeftButtonDown += OnMapLeftButtonDown;
        MapView.PreviewMouseLeftButtonUp += OnMapLeftButtonUp;
        MapView.MouseLeave += (_, _) => _viewModel.UpdatePointer(null);
    }

    /// <summary>Sizes the scale bar for the current zoom and latitude (at most <see cref="MaxScaleBarPixels"/> wide).</summary>
    private void OnViewportChanged()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(OnViewportChanged);
            return;
        }

        var viewport = MapView.Map.Navigator.Viewport;
        if (ScaleBarLength.For(viewport.Resolution, viewport.CenterY, MaxScaleBarPixels) is not { } scale)
        {
            return;
        }

        _map.UpdateGrid(_viewModel.ShowUtmGrid, _viewModel.CoordinateDatum);
        ScaleBarLabel.Text = scale.Label;
        ScaleBarRuler.Width = Math.Round(scale.Pixels);
        ScaleBarRuler.Segments = scale.Segments;
    }

    /// <summary>Asks about unsaved documents before closing (APP-04). Cancelling keeps everything open.</summary>
    /// <summary>
    /// Asks about unsaved documents (APP-04). When the answer is known at once (nothing modified, or Discard/Cancel),
    /// the close goes ahead or stops right here: calling Close() again while the window is closing throws.
    /// Only if saving is still running is the close postponed and repeated afterwards.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_closeConfirmed || e.Cancel)
        {
            return;
        }

        var confirmation = _viewModel.ConfirmCloseAllAsync();
        if (confirmation.IsCompleted)
        {
            _closeConfirmed = confirmation.Result;
            e.Cancel = !_closeConfirmed;
            return;
        }

        e.Cancel = true;
        CloseWhenConfirmed(confirmation);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.CancelOperation();
        _map.Dispose();
        base.OnClosed(e);
    }

    /// <summary>async void on purpose: an unexpected error reaches the global handler (NFR-03) instead of being lost.</summary>
    private async void CloseWhenConfirmed(Task<bool> confirmation)
    {
        if (await confirmation)
        {
            _closeConfirmed = true;
            await Dispatcher.InvokeAsync(Close);
        }
    }

    private void OnDocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (DocumentViewModel removed in e.OldItems ?? Array.Empty<DocumentViewModel>())
        {
            _map.RemoveDocument(removed);
            foreach (var window in _profileWindows.Where(w => ReferenceEquals(w.ViewModel.Document, removed)).ToList())
            {
                window.Close();
            }

        }

        foreach (DocumentViewModel added in e.NewItems ?? Array.Empty<DocumentViewModel>())
        {
            _map.ShowDocument(added);
            added.ContentChanged += (_, _) =>
            {
                _map.ShowDocument(added);
                ShowSelection(ensureVisible: false);
            };
            added.SelectionChanged += (_, _) =>
            {
                if (ReferenceEquals(added, _viewModel.ActiveDocument))
                {
                    ShowSelection(ensureVisible: true);
                }
            };
            added.LayerVisibilityChanged += (_, item) => _map.SetVisibility(added, item);
        }

        MapView.RefreshGraphics();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.SelectedMapProvider):
                _map.SetProvider(_viewModel.SelectedMapProvider);
                break;
            case nameof(MainViewModel.ShowUtmGrid) or nameof(MainViewModel.CoordinateDatum):
                _map.UpdateGrid(_viewModel.ShowUtmGrid, _viewModel.CoordinateDatum);
                MapView.RefreshGraphics();
                break;
            case nameof(MainViewModel.IsDrawing):
                MapView.Cursor = _viewModel.IsDrawing ? Cursors.Cross : null;
                break;
            case nameof(MainViewModel.TrackColoring):
                _map.SetTrackColoring(_viewModel.TrackColoring, _viewModel.Documents);
                MapView.RefreshGraphics();
                break;
            case nameof(MainViewModel.ActiveDocument):
                ShowSelection(ensureVisible: false);
                break;
        }
    }

    /// <summary>
    /// Shows the profile window of a track or route (PROF-01); if it is already open, brings it to the front.
    /// The point under the pointer in the profile is marked on the map (PROF-04).
    /// </summary>
    private void ShowProfile(ProfileViewModel profile)
    {
        var existing = _profileWindows.FirstOrDefault(w =>
            ReferenceEquals(w.ViewModel.Document, profile.Document) && ReferenceEquals(w.ViewModel.Layer, profile.Layer));
        if (existing is not null)
        {
            profile.Detach();
            existing.Activate();
            return;
        }

        var window = new ProfileWindow(profile) { Owner = this };
        profile.HoveredPointChanged += (_, point) => _map.ShowHover(point?.Position);
        window.Closed += (_, _) => _profileWindows.Remove(window);
        _profileWindows.Add(window);
        window.Show();
    }

    /// <summary>Highlights the selected points of the active document; optionally scrolls the map to them.</summary>
    private void ShowSelection(bool ensureVisible)
    {
        var points = _viewModel.ActiveDocument?.SelectedPoints.Select(p => p.Position).ToList() ?? [];
        _map.ShowSelection(points);
        if (ensureVisible && points.Count > 0)
        {
            _map.EnsureVisible(points[0]);
        }
    }

    private static string[]? DroppedFiles(DragEventArgs e) =>
        e.Data.GetDataPresent(DataFormats.FileDrop) ? e.Data.GetData(DataFormats.FileDrop) as string[] : null;

    /// <summary>The cursor shows "copy" only when at least one dropped file can be opened, and only while not busy.</summary>
    private void OnFileDragOver(object sender, DragEventArgs e)
    {
        if (DroppedFiles(e) is not { } files)
        {
            return; // not files (e.g. reordering user cartographies): let the controls handle it
        }

        e.Effects = !_viewModel.IsBusy && MainViewModel.CanOpenAny(files) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnFileDrop(object sender, DragEventArgs e)
    {
        if (DroppedFiles(e) is not { } files)
        {
            return;
        }

        e.Handled = true;
        if (_viewModel.IsBusy)
        {
            return;
        }

        Activate();
        await _viewModel.OpenFilesAsync(files);
    }

    /// <summary>The user cartographies list closes when clicking elsewhere: the button must pop up again.</summary>
    private void OnLocalMapsPopupClosed(object? sender, EventArgs e) => LocalMapsButton.IsChecked = false;

    private Point? _localMapDragStart;

    /// <summary>Dragging starts only from the ≡ handle, so the check box, slider and buttons keep working (LMAP-11).</summary>
    private void OnLocalMapHandleDown(object sender, MouseButtonEventArgs e) => _localMapDragStart = e.GetPosition(LocalMapsList);

    private void OnLocalMapHandleMove(object sender, MouseEventArgs e)
    {
        if (_localMapDragStart is not { } start || e.LeftButton != MouseButtonState.Pressed
            || sender is not FrameworkElement { DataContext: LocalMapItemViewModel item })
        {
            return;
        }

        var delta = e.GetPosition(LocalMapsList) - start;
        if (Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance && Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance)
        {
            return;
        }

        _localMapDragStart = null;
        DragDrop.DoDragDrop((DependencyObject)sender, item, DragDropEffects.Move);
    }

    private void OnLocalMapsDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(LocalMapItemViewModel)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>Dropped on another cartography: takes its place in the list (and in the drawing order).</summary>
    private void OnLocalMapsDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(LocalMapItemViewModel)) is not LocalMapItemViewModel dragged)
        {
            return;
        }

        var target = (e.OriginalSource as DependencyObject) is { } source
            ? System.Windows.Controls.ItemsControl.ContainerFromElement(LocalMapsList, source) as System.Windows.Controls.ListBoxItem
            : null;
        var to = target?.DataContext is LocalMapItemViewModel over ? _viewModel.LocalMaps.IndexOf(over) : _viewModel.LocalMaps.Count - 1;
        _viewModel.MoveLocalMap(_viewModel.LocalMaps.IndexOf(dragged), to);
        LocalMapsList.SelectedItem = dragged;
        e.Handled = true;
    }

    /// <summary>Ctrl+↑ / Ctrl+↓ move the selected cartography up or down (keyboard alternative to dragging).</summary>
    private void OnLocalMapsKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control || LocalMapsList.SelectedItem is not LocalMapItemViewModel item || e.Key is not (Key.Up or Key.Down))
        {
            return;
        }

        var from = _viewModel.LocalMaps.IndexOf(item);
        _viewModel.MoveLocalMap(from, from + (e.Key == Key.Up ? -1 : 1));
        LocalMapsList.SelectedItem = item;
        (LocalMapsList.ItemContainerGenerator.ContainerFromItem(item) as UIElement)?.Focus();
        e.Handled = true;
    }

    private void OnMapPointerMoved(object? sender, MapEventArgs e)
    {
        var world = e.WorldPosition;
        var position = world is null ? (GeoPosition?)null : WebMercator.ToGeo(world.X, world.Y);
        _viewModel.UpdatePointer(position);

        if (_drag is { } drag && position is { } p)
        {
            if (!drag.Started && e.ScreenPosition.Distance(drag.Start) < MinimumDragPixels)
            {
                return;
            }

            drag.Started = true;
            drag.Target = p;
            _map.ShowSelection([p]);
            e.Handled = true;
        }
    }

    /// <summary>Pressing on the selected point starts a drag (EDIT-04); only an already selected point can be moved.</summary>
    private void OnMapPointerPressed(object? sender, MapEventArgs e)
    {
        // Ctrl+click adds a point (OnMapTapped); it must not start dragging the selected one.
        if (Mouse.LeftButton != MouseButtonState.Pressed || IsControlPressed || e.WorldPosition is null
            || _viewModel.ActiveDocument is not { SelectedRows.Count: 1 } doc)
        {
            return;
        }

        if (_map.HitTest(e.WorldPosition) is { } hit && ReferenceEquals(hit.Document, doc) && ReferenceEquals(hit.Point, doc.SelectedRows[0].Point))
        {
            _drag = new DragState(doc, hit.Point, e.ScreenPosition);
            _map.LockPan(true);
            e.Handled = true;
        }
    }

    private void OnMapPointerReleased(object? sender, MapEventArgs e)
    {
        if (_drag is not { } drag)
        {
            return;
        }

        _drag = null;
        _map.LockPan(false);
        if (drag.Started && drag.Target is { } target)
        {
            _viewModel.MovePoint(drag.Document, drag.Point, target);
            e.Handled = true;
        }
        else
        {
            ShowSelection(ensureVisible: false);
        }
    }

    /// <summary>
    /// A click selects the nearest point within a few pixels, or clears the selection (LAY-03).
    /// Ctrl+click adds a point there to the active document (APP-08).
    /// </summary>
    private void OnMapTapped(object? sender, MapEventArgs e)
    {
        if (e.WorldPosition is null || IsControlPressed || _viewModel.IsDrawing)
        {
            return;
        }

        SelectAt(e.WorldPosition);
    }

    private static bool IsControlPressed => (Keyboard.Modifiers & ModifierKeys.Control) != 0;

    /// <summary>
    /// Ctrl+click adds a point (APP-08). Handled on the WPF mouse events, before Mapsui sees them, so it does not
    /// depend on Mapsui's tap detection and the press does not start panning the map.
    /// </summary>
    private void OnMapLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _addPointPress = null;
        _drawPress = null;
        if (IsControlPressed && _viewModel.ActiveDocument is not null)
        {
            _addPointPress = e.GetPosition(MapView);
            e.Handled = true;
        }
        else if (_viewModel.IsDrawing)
        {
            // Drawing mode: not handled, so dragging still pans the map; a click without moving adds the point on release.
            _drawPress = e.GetPosition(MapView);
        }
    }

    private void OnMapLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_drawPress is { } drawPress)
        {
            _drawPress = null;
            var at = e.GetPosition(MapView);
            // A press on the selected point may be a drag of that point (OnMapPointerPressed): that moves it instead.
            if (_drag is null && (at - drawPress).Length < MinimumDragPixels)
            {
                var point = MapView.Map.Navigator.Viewport.ScreenToWorld(at.X, at.Y);
                _viewModel.AddPointAt(WebMercator.ToGeo(point.X, point.Y));
            }

            return;
        }

        if (_addPointPress is not { } press)
        {
            return;
        }

        _addPointPress = null;
        e.Handled = true;
        var screen = e.GetPosition(MapView);
        if ((screen - press).Length < MinimumDragPixels)
        {
            var world = MapView.Map.Navigator.Viewport.ScreenToWorld(screen.X, screen.Y);
            _viewModel.AddPointAt(WebMercator.ToGeo(world.X, world.Y));
        }
    }

    /// <summary>
    /// Right click selects the point under the pointer before the context menu opens. Over an empty spot it keeps
    /// the selection, so the menu can insert a point there after the selected one.
    /// </summary>
    private void OnMapRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var screen = e.GetPosition(MapView);
        var world = MapView.Map.Navigator.Viewport.ScreenToWorld(screen.X, screen.Y);
        _contextPosition = WebMercator.ToGeo(world.X, world.Y);
        _contextOnPoint = false;
        if (_map.HitTest(world) is { } hit)
        {
            _viewModel.SelectFromMap(hit.Document, hit.Point);
            _contextOnPoint = true;
        }
    }

    /// <summary>
    /// Builds the map's context menu from what is under the pointer: on a waypoint, edit or delete it; with a track
    /// point selected, the point and track commands plus "add waypoint"; elsewhere, only "add waypoint".
    /// Items carry a Tag with their group: "waypoint", "track" or "add".
    /// </summary>
    private void OnMapContextMenuOpening(object sender, System.Windows.Controls.ContextMenuEventArgs e)
    {
        string[] groups = _contextOnPoint && _viewModel.HasWaypointSelected ? ["waypoint"]
            : _viewModel.HasTrackPointSelected ? ["track", "add"]
            : _viewModel.ActiveDocument is not null && !_viewModel.IsBusy ? ["add"]
            : [];
        if (groups.Length == 0 || MapView.ContextMenu is not { } menu)
        {
            e.Handled = true;
            return;
        }

        foreach (var item in menu.Items.OfType<FrameworkElement>())
        {
            item.Visibility = groups.Contains(item.Tag as string) ? Visibility.Visible : Visibility.Collapsed;
        }

        MapInsertPointItem.IsEnabled = _viewModel.ActiveDocument?.SelectedRows.Count == 1;
    }

    private void OnMapAddWaypointClick(object sender, RoutedEventArgs e)
    {
        if (_contextPosition is { } position)
        {
            _viewModel.AddWaypointAt(position);
        }
    }

    /// <summary>
    /// Inserts like Ctrl+click at the right-clicked spot, after the selected point. Right-clicked on a point,
    /// it inserts the midpoint after it instead (a point at the same place would be useless).
    /// </summary>
    private void OnMapInsertPointClick(object sender, RoutedEventArgs e)
    {
        if (_contextOnPoint || _contextPosition is not { } position)
        {
            _viewModel.InsertPoint();
        }
        else
        {
            _viewModel.AddPointAt(position);
        }
    }

    private void SelectAt(MPoint world)
    {
        if (_map.HitTest(world) is { } hit)
        {
            _viewModel.SelectFromMap(hit.Document, hit.Point);
        }
        else if (_viewModel.ActiveDocument is { } active)
        {
            _viewModel.SelectFromMap(active, null);
        }
    }

    private void OnAttributionNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private sealed class DragState(DocumentViewModel document, TrailPoint point, Mapsui.Manipulations.ScreenPosition start)
    {
        public DocumentViewModel Document => document;

        public TrailPoint Point => point;

        public Mapsui.Manipulations.ScreenPosition Start => start;

        public bool Started { get; set; }

        public GeoPosition? Target { get; set; }
    }
}
