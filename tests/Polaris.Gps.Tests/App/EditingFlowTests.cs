using Polaris.Gps.App.ViewModels;
using Polaris.Gps.Domain.Documents;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Tests.Domain;
using Polaris.Gps.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Polaris.Gps.Tests.App;

/// <summary>Editing flows through the view models, as the UI drives them (increment 3).</summary>
public sealed class EditingFlowTests
{
    private const string Path = @"C:\data\track.gpx";

    private readonly FakeDialogService _dialogs = new();
    private readonly FakeDocumentStore _store = new();
    private readonly MainViewModel _vm;
    private readonly TrailData _data = new();
    private readonly Track _track = new() { Name = "Subida" };

    public EditingFlowTests()
    {
        // Track with two segments (10 + 3 points), a route and two waypoints.
        var first = new TrackSegment();
        first.Points.AddRange(EditingTests.Points(10));
        var second = new TrackSegment();
        second.Points.AddRange(EditingTests.Points(3));
        _track.Segments.AddRange([first, second]);
        _data.Tracks.Add(_track);
        var route = new Route { Name = "Plan" };
        route.Points.AddRange(EditingTests.Points(4));
        _data.Routes.Add(route);
        _data.Waypoints.AddRange(EditingTests.Points(2));
        _store.Contents[Path] = _data;

        _vm = new MainViewModel(_dialogs, _store, new FakeSettingsStore(), NullLogger<MainViewModel>.Instance);
        _dialogs.OpenPaths.Enqueue(Path);
        _vm.OpenAsync(DocumentFormat.Gpx).GetAwaiter().GetResult();
    }

    private DocumentViewModel Doc => _vm.ActiveDocument!;

    private List<TrailPoint> FirstSegment => _track.Segments[0].Points;

    private void Select(params PointRowViewModel[] rows) => Doc.SetSelectedRows(rows);

    [Fact]
    public void Layers_list_tracks_routes_and_waypoints_with_statistics()
    {
        Assert.Equal([LayerKind.Track, LayerKind.Route, LayerKind.Waypoints], Doc.Layers.Select(l => l.Kind));
        Assert.Equal("Subida", Doc.Layers[0].Name);
        Assert.StartsWith("13 puntos · ", Doc.Layers[0].Details);
        Assert.EndsWith(" km", Doc.Layers[0].Details);
        Assert.Same(Doc.Layers[0], Doc.SelectedLayer);
    }

    [Fact]
    public void Hiding_a_layer_notifies_the_map_and_is_not_a_document_change()
    {
        LayerItemViewModel? hidden = null;
        Doc.LayerVisibilityChanged += (_, item) => hidden = item;

        Doc.Layers[1].IsVisible = false;

        Assert.Same(Doc.Layers[1], hidden);
        Assert.False(Doc.IsDirty);
    }

    [Fact]
    public void Rows_number_points_by_segment_when_there_are_several()
    {
        Assert.Equal(13, Doc.Rows.Count);
        Assert.Equal("1·1", Doc.Rows[0].Number);
        Assert.Equal("2·3", Doc.Rows[12].Number);
    }

    [Fact]
    public void Map_context_menu_needs_a_selected_track_point()
    {
        Assert.False(_vm.HasTrackPointSelected);

        _vm.SelectFromMap(Doc, FirstSegment[3]);
        Assert.True(_vm.HasTrackPointSelected);

        _vm.SelectFromMap(Doc, _data.Routes[0].Points[1]);
        Assert.False(_vm.HasTrackPointSelected);

        _vm.SelectFromMap(Doc, _data.Waypoints[0]);
        Assert.False(_vm.HasTrackPointSelected);
    }

    [Fact]
    public void Adding_a_waypoint_asks_for_its_data_and_can_be_undone()
    {
        var position = new GeoPosition(43.1, -2.5);
        _dialogs.ToolDialog = vm =>
        {
            var waypoint = Assert.IsType<WaypointViewModel>(vm);
            Assert.Equal("Waypoint 3", waypoint.Name);
            waypoint.Name = "  Fuente  ";
            waypoint.Description = "Agua todo el año";
            return true;
        };

        _vm.AddWaypointAt(position);

        var added = _data.Waypoints[^1];
        Assert.Equal(3, _data.Waypoints.Count);
        Assert.Equal("Fuente", added.Name);
        Assert.Equal("Agua todo el año", added.Description);
        Assert.Null(added.Comment);
        Assert.Equal(position, added.Position);
        Assert.Same(added, Assert.Single(Doc.SelectedRows).Point);
        Assert.True(_vm.HasWaypointSelected);

        _vm.Undo();
        Assert.Equal(2, _data.Waypoints.Count);
    }

    [Fact]
    public void Cancelling_the_waypoint_dialog_adds_nothing()
    {
        _dialogs.ToolDialog = _ => false;

        _vm.AddWaypointAt(new GeoPosition(43.1, -2.5));

        Assert.Equal(2, _data.Waypoints.Count);
        Assert.False(Doc.IsDirty);
    }

    [Fact]
    public void Adding_the_first_waypoint_creates_the_waypoints_layer()
    {
        _vm.SelectFromMap(Doc, _data.Waypoints[0]);
        _dialogs.ConfirmAnswer = true;
        _vm.DeleteSelection();
        _vm.SelectFromMap(Doc, _data.Waypoints[0]);
        _vm.DeleteSelection();
        Assert.DoesNotContain(Doc.Layers, l => l.Kind == LayerKind.Waypoints);
        _dialogs.ToolDialog = _ => true;

        _vm.AddWaypointAt(new GeoPosition(43.1, -2.5));

        Assert.Equal(LayerKind.Waypoints, Doc.SelectedLayer!.Kind);
        Assert.Single(_data.Waypoints);
    }

    [Fact]
    public void Editing_a_waypoint_changes_its_texts_in_one_undoable_step()
    {
        var waypoint = _data.Waypoints[1];
        var position = waypoint.Position;
        _vm.SelectFromMap(Doc, waypoint);
        Assert.True(_vm.EditWaypointCommand.CanExecute(null));
        _dialogs.ToolDialog = vm =>
        {
            ((WaypointViewModel)vm).Name = "Collado";
            ((WaypointViewModel)vm).Comment = "Cruce";
            return true;
        };

        _vm.EditWaypoint();

        Assert.Equal("Collado", waypoint.Name);
        Assert.Equal("Cruce", waypoint.Comment);
        Assert.Equal(position, waypoint.Position);
        _vm.Undo();
        Assert.NotEqual("Collado", waypoint.Name);
    }

    [Fact]
    public void Waypoint_commands_need_a_selected_waypoint()
    {
        _vm.SelectFromMap(Doc, FirstSegment[2]);

        Assert.False(_vm.HasWaypointSelected);
        Assert.False(_vm.EditWaypointCommand.CanExecute(null));
    }

    [Fact]
    public void Deleting_a_waypoint_always_asks_for_confirmation()
    {
        var waypoint = _data.Waypoints[0];
        waypoint.Name = "Refugio";
        _vm.SelectFromMap(Doc, waypoint);
        _dialogs.ConfirmAnswer = false;

        _vm.DeleteSelection();

        Assert.Contains("Refugio", Assert.Single(_dialogs.Confirmations));
        Assert.Equal(2, _data.Waypoints.Count);

        _dialogs.ConfirmAnswer = true;
        _vm.DeleteSelection();

        Assert.Single(_data.Waypoints);
        Assert.DoesNotContain(waypoint, _data.Waypoints);
    }

    [Fact]
    public void Selecting_a_point_from_the_map_shows_its_layer_and_detail()
    {
        var waypoint = _data.Waypoints[1];
        var selectionEvents = 0;
        Doc.SelectionChanged += (_, _) => selectionEvents++;

        _vm.SelectFromMap(Doc, waypoint);

        Assert.Equal(LayerKind.Waypoints, Doc.SelectedLayer!.Kind);
        Assert.Same(waypoint, Assert.Single(Doc.SelectedRows).Point);
        Assert.Same(waypoint, Doc.Detail!.Point);
        Assert.True(selectionEvents > 0);

        _vm.SelectFromMap(Doc, null);
        Assert.Empty(Doc.SelectedRows);
        Assert.Null(Doc.Detail);
    }

    [Fact]
    public void Delete_removes_the_point_selects_the_next_and_undo_restores_it()
    {
        var fifth = FirstSegment[4];
        var sixth = FirstSegment[5];
        Select(Doc.Rows[4]);

        _vm.DeleteSelection();

        Assert.Equal(9, FirstSegment.Count);
        Assert.DoesNotContain(fifth, FirstSegment);
        Assert.Same(sixth, Assert.Single(Doc.SelectedRows).Point);
        Assert.True(Doc.IsDirty);
        Assert.Empty(_dialogs.Confirmations); // one point: no confirmation (EDIT-02)
        Assert.Equal("Eliminados 1 puntos.", _vm.StatusMessage);

        _vm.Undo();

        Assert.Equal(10, FirstSegment.Count);
        Assert.Same(fifth, FirstSegment[4]);
        Assert.False(Doc.IsDirty);
    }

    [Fact]
    public void Deleting_points_of_several_segments_is_one_undo_step()
    {
        Select(Doc.Rows[0], Doc.Rows[11]);

        _vm.DeleteSelection();
        Assert.Equal(9, FirstSegment.Count);
        Assert.Equal(2, _track.Segments[1].Points.Count);

        _vm.Undo();
        Assert.Equal(13, _track.PointCount);
        Assert.False(Doc.CanUndo);
    }

    [Fact]
    public void Deleting_more_than_50_points_asks_and_cancelling_keeps_them()
    {
        var segment = new TrackSegment();
        segment.Points.AddRange(EditingTests.Points(60));
        Doc.Document.Execute(new Polaris.Gps.Domain.Editing.CompositeCommand([]));
        _track.Segments.Add(segment);
        Doc.SelectedLayer = Doc.Layers[1];
        Doc.SelectedLayer = Doc.Layers[0];
        Select(Doc.Rows.Where(r => r.Owner == segment.Points).ToArray());
        _dialogs.ConfirmAnswer = false;

        _vm.DeleteSelection();

        Assert.Single(_dialogs.Confirmations);
        Assert.Equal(60, segment.Points.Count);
    }

    [Fact]
    public void With_no_point_selected_delete_removes_the_whole_track_after_confirmation()
    {
        _dialogs.ConfirmAnswer = false;
        _vm.DeleteSelection();
        Assert.Single(_data.Tracks);
        Assert.Contains("«Subida» con 13 puntos", _dialogs.Confirmations[0]);

        _dialogs.ConfirmAnswer = true;
        _vm.DeleteSelection();
        Assert.Empty(_data.Tracks);
        Assert.Equal(LayerKind.Route, Doc.Layers[0].Kind);

        _vm.Undo();
        Assert.Same(_track, _data.Tracks.Single());
        Assert.Equal(3, Doc.Layers.Count);
    }

    [Fact]
    public void Without_a_usable_selection_delete_and_insert_only_show_a_hint()
    {
        Doc.SelectedLayer = Doc.Layers[2]; // waypoints, nothing selected

        _vm.DeleteSelection();
        Assert.Equal("Selecciona un punto o un track.", _vm.StatusMessage);

        _vm.InsertPoint();
        Assert.Equal("Para insertar, selecciona un único punto de un track o de una ruta. Para añadir puntos nuevos, usa Control + clic en el mapa.", _vm.StatusMessage);

        Select(Doc.Rows[0]);
        _vm.InsertPoint(); // waypoints are not a sequence
        Assert.Equal(2, _data.Waypoints.Count);
        Assert.False(Doc.IsDirty);
    }

    [Fact]
    public void Insert_adds_a_marked_midpoint_after_the_selected_point_and_selects_it()
    {
        var third = FirstSegment[2];
        Select(Doc.Rows[2]);

        _vm.InsertPoint();

        Assert.Equal(11, FirstSegment.Count);
        Assert.Same(third, FirstSegment[2]);
        var added = FirstSegment[3];
        Assert.True(added.IsAdded);
        Assert.Same(added, Assert.Single(Doc.SelectedRows).Point);
        Assert.True(Doc.Rows[3].IsAdded);

        _vm.Undo();
        Assert.Equal(10, FirstSegment.Count);
    }

    [Fact]
    public void Moving_a_point_can_be_undone()
    {
        var point = FirstSegment[0];
        var original = point.Position;

        _vm.MovePoint(Doc, point, new GeoPosition(43, -2));

        Assert.Equal(new GeoPosition(43, -2), point.Position);
        Assert.Equal("Punto movido.", _vm.StatusMessage);
        _vm.Undo();
        Assert.Equal(original, point.Position);
    }

    [Fact]
    public void Point_detail_changes_apply_only_when_valid()
    {
        var waypoint = _data.Waypoints[0];
        _vm.SelectFromMap(Doc, waypoint);
        Doc.Detail!.Name = "Cima";
        Doc.Detail.Latitude = "91";

        _vm.ApplyPointDetail();
        Assert.Null(waypoint.Name);
        Assert.NotNull(Doc.Detail.Error);
        Assert.False(Doc.IsDirty);

        Doc.Detail.Latitude = "43,5";
        _vm.ApplyPointDetail();
        Assert.Equal("Cima", waypoint.Name);
        Assert.Equal(43.5, waypoint.Position.Latitude);
        Assert.Equal("Cima", Doc.Rows[0].Name); // list refreshed (EDIT-07)

        _vm.Undo();
        Assert.Null(waypoint.Name);
    }

    [Fact]
    public void Undo_and_redo_commands_follow_the_history()
    {
        Assert.False(_vm.UndoCommand.CanExecute(null));
        Select(Doc.Rows[0]);
        _vm.DeleteSelection();
        Assert.True(_vm.UndoCommand.CanExecute(null));
        Assert.False(_vm.RedoCommand.CanExecute(null));

        _vm.UndoCommand.Execute(null);
        Assert.True(_vm.RedoCommand.CanExecute(null));
        _vm.RedoCommand.Execute(null);
        Assert.Equal(9, FirstSegment.Count);
    }

    [Fact]
    public void Edits_refresh_counts_layers_and_notify_the_map_once()
    {
        var mapUpdates = 0;
        Doc.ContentChanged += (_, _) => mapUpdates++;
        Select(Doc.Rows[0]);

        _vm.DeleteSelection();

        Assert.Equal(1, mapUpdates);
        Assert.StartsWith("12 puntos", Doc.Layers[0].Details);
        Assert.Contains("Puntos: 18", Doc.Counts);
        Assert.Equal(12, Doc.Rows.Count);
    }
}
