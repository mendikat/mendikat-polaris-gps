using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using Mapsui;
using Mapsui.Extensions;
using Mendikat.TrailLab.App.Map;
using Mendikat.TrailLab.App.ViewModels;
using Mendikat.TrailLab.App.Views;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Domain.Model;
using Mendikat.TrailLab.Mapping.Coordinates;

namespace Mendikat.TrailLab.App;

/// <summary>
/// Main window. Code-behind only wires the Mapsui control (a view concern) to the view model:
/// layers follow the documents, map requests are executed, pointer coordinates are reported, and
/// map clicks / drags / right clicks select or move points (LAY-03, EDIT-04).
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>A press must move at least this many pixels to become a drag; smaller movements never move a point.</summary>
    private const double MinimumDragPixels = 4;

    private readonly MainViewModel _viewModel;
    private readonly MapController _map;
    private bool _closeConfirmed;
    private DragState? _drag;
    private System.Windows.Point? _addPointPress;
    private readonly List<ProfileWindow> _profileWindows = [];

    public MainWindow(MainViewModel viewModel, Func<Mapsui.Map, MapController> createMapController)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        MapView.Map = new Mapsui.Map();
        _map = createMapController(MapView.Map);
        _map.SetProvider(viewModel.SelectedMapProvider);
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
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        MapView.MapPointerMoved += OnMapPointerMoved;
        MapView.MapPointerPressed += OnMapPointerPressed;
        MapView.MapPointerReleased += OnMapPointerReleased;
        MapView.MapTapped += OnMapTapped;
        MapView.PreviewMouseRightButtonDown += OnMapRightButtonDown;
        MapView.PreviewMouseLeftButtonDown += OnMapLeftButtonDown;
        MapView.PreviewMouseLeftButtonUp += OnMapLeftButtonUp;
        MapView.MouseLeave += (_, _) => _viewModel.UpdatePointer(null);
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
        if (e.WorldPosition is null || IsControlPressed)
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
        if (IsControlPressed && _viewModel.ActiveDocument is not null)
        {
            _addPointPress = e.GetPosition(MapView);
            e.Handled = true;
        }
    }

    private void OnMapLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
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

    /// <summary>Right click selects the point under the pointer before the context menu opens.</summary>
    private void OnMapRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var screen = e.GetPosition(MapView);
        SelectAt(MapView.Map.Navigator.Viewport.ScreenToWorld(screen.X, screen.Y));
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
