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

    public MainWindow(MainViewModel viewModel, Func<Mapsui.Map, MapController> createMapController)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        MapView.Map = new Mapsui.Map();
        _map = createMapController(MapView.Map);
        _map.SetProvider(viewModel.SelectedMapProvider);
        _map.TileStatusChanged += (_, failed) => Dispatcher.BeginInvoke(() => _viewModel.ReportMapTiles(failed));

        viewModel.ExitRequested += (_, _) => Close();
        viewModel.MapRequested += (_, request) => _map.Handle(request);
        viewModel.Documents.CollectionChanged += OnDocumentsChanged;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        MapView.MapPointerMoved += OnMapPointerMoved;
        MapView.MapPointerPressed += OnMapPointerPressed;
        MapView.MapPointerReleased += OnMapPointerReleased;
        MapView.MapTapped += OnMapTapped;
        MapView.PreviewMouseRightButtonDown += OnMapRightButtonDown;
        MapView.MouseLeave += (_, _) => _viewModel.UpdatePointer(null);
    }

    /// <summary>Asks about unsaved documents before closing (APP-04). Cancelling keeps everything open.</summary>
    protected override async void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_closeConfirmed)
        {
            return;
        }

        e.Cancel = true;
        if (await _viewModel.ConfirmCloseAllAsync())
        {
            _closeConfirmed = true;
            _map.Dispose();
            Close();
        }
    }

    private void OnDocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (DocumentViewModel removed in e.OldItems ?? Array.Empty<DocumentViewModel>())
        {
            _map.RemoveDocument(removed);
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
        if (Mouse.LeftButton != MouseButtonState.Pressed || e.WorldPosition is null
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

    /// <summary>A click selects the nearest point within a few pixels, or clears the selection (LAY-03).</summary>
    private void OnMapTapped(object? sender, MapEventArgs e)
    {
        if (e.WorldPosition is null)
        {
            return;
        }

        SelectAt(e.WorldPosition);
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
