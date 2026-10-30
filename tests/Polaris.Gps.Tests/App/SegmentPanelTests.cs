using Polaris.Gps.App.ViewModels;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Polaris.Gps.Tests.App;

/// <summary>Layers panel: the segments of a track in parts can be selected and deleted one by one.</summary>
public sealed class SegmentPanelTests
{
    private readonly FakeDialogService _dialogs = new();
    private readonly MainViewModel _vm;
    private readonly Track _track = new() { Name = "Travesía" };

    public SegmentPanelTests()
    {
        _vm = new MainViewModel(_dialogs, new FakeDocumentStore(), new FakeSettingsStore(), NullLogger<MainViewModel>.Instance);
        _vm.NewGpx();
        var data = _vm.ActiveDocument!.Document.Data;
        data.Tracks.Clear();
        for (var s = 0; s < 3; s++)
        {
            var segment = new TrackSegment();
            segment.Points.AddRange(Enumerable.Range(0, s + 2).Select(i => new TrailPoint(new GeoPosition(43 + s * 0.01 + i * 0.001, -2))));
            _track.Segments.Add(segment);
        }

        data.Tracks.Add(_track);
        _vm.ActiveDocument.Document.MarkChanged();
    }

    private LayerItemViewModel Layer => _vm.ActiveDocument!.Layers.Single(l => ReferenceEquals(l.Model, _track));

    [Fact]
    public void A_track_in_parts_lists_its_segments_and_one_can_be_selected()
    {
        Assert.Equal(["Segmento 1 · 2 puntos", "Segmento 2 · 3 puntos", "Segmento 3 · 4 puntos"],
            Layer.Segments.Select(s => s.Label[..s.Label.LastIndexOf(" · ", StringComparison.Ordinal)]));

        _vm.SelectSegmentCommand.Execute(Layer.Segments[1]);

        Assert.Equal(_track.Segments[1].Points, _vm.ActiveDocument!.SelectedPoints);
    }

    [Fact]
    public void Deleting_a_segment_asks_keeps_the_others_and_can_be_undone()
    {
        var second = _track.Segments[1];

        _vm.DeleteSegmentCommand.Execute(Layer.Segments[1]);

        Assert.StartsWith("¿Eliminar el segmento 2 de «Travesía» (3 puntos)?", Assert.Single(_dialogs.Confirmations));
        Assert.Equal(2, _track.Segments.Count);
        Assert.DoesNotContain(second, _track.Segments);
        Assert.Equal(2, Layer.Segments.Count);

        _vm.Undo();
        Assert.Same(second, _track.Segments[1]);
    }

    [Fact]
    public void Nothing_is_deleted_when_the_user_cancels_and_a_single_segment_is_not_listed()
    {
        _dialogs.ConfirmAnswer = false;
        _vm.DeleteSegmentCommand.Execute(Layer.Segments[0]);
        Assert.Equal(3, _track.Segments.Count);

        _dialogs.ConfirmAnswer = true;
        _vm.DeleteSegmentCommand.Execute(Layer.Segments[0]);
        _vm.DeleteSegmentCommand.Execute(Layer.Segments[0]);
        Assert.Single(_track.Segments);
        Assert.False(Layer.HasSegments);
    }
}

/// <summary>The symbol of a waypoint in the point detail panel.</summary>
public sealed class WaypointDetailSymbolTests
{
    [Fact]
    public void The_detail_of_a_waypoint_shows_its_symbol_and_changing_it_is_an_edit()
    {
        var point = new TrailPoint(new GeoPosition(43, -2)) { Name = "Refugio", Symbol = "lodging" };

        var detail = new PointDetailViewModel(point, isWaypoint: true);
        Assert.True(detail.IsWaypoint);
        Assert.Equal("Lodging", detail.Symbol.GpxName);
        Assert.False(detail.HasChanges);
        Assert.True(detail.TryBuild(out var same));
        Assert.Equal("lodging", same.Symbol); // untouched: kept exactly as in the file

        detail.Symbol = detail.Symbols.Single(s => s.GpxName == "Summit");
        Assert.True(detail.HasChanges);
        Assert.True(detail.TryBuild(out var changed));
        Assert.Equal("Summit", changed.Symbol);
        Assert.False(new PointDetailViewModel(point).IsWaypoint);
    }
}
