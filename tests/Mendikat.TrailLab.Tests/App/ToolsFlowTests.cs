using Mendikat.TrailLab.App.ViewModels;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Domain.Model;
using Mendikat.TrailLab.Domain.Processing;
using Mendikat.TrailLab.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mendikat.TrailLab.Tests.App;

/// <summary>Simplify and regenerate times through the view models (increment 6).</summary>
public sealed class ToolsFlowTests
{
    private const string Path = @"C:\data\tools.gpx";

    private readonly FakeDialogService _dialogs = new();
    private readonly FakeDocumentStore _store = new();
    private readonly MainViewModel _vm;
    private readonly TrailData _data = new();
    private readonly List<TrailPoint> _points;

    public ToolsFlowTests()
    {
        // 50 points on a straight north-south line, 10 m apart, with a time on the first one.
        _points = Enumerable.Range(0, 50).Select(i => new TrailPoint(new GeoPosition(42.8 + i * 0.00009, -1.6))).ToList();
        _points[0].Time = new DateTimeOffset(2026, 5, 1, 7, 0, 0, TimeSpan.Zero);
        var segment = new TrackSegment();
        segment.Points.AddRange(_points);
        var track = new Track { Name = "Recta" };
        track.Segments.Add(segment);
        _data.Tracks.Add(track);
        _data.Waypoints.Add(new TrailPoint(new GeoPosition(42.8, -1.6)) { Time = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero) });
        _store.Contents[Path] = _data;
        _vm = new MainViewModel(_dialogs, _store, new FakeSettingsStore(), NullLogger<MainViewModel>.Instance);
        _dialogs.OpenPaths.Enqueue(Path);
        _vm.OpenAsync(DocumentFormat.Gpx).GetAwaiter().GetResult();
    }

    private List<TrailPoint> TrackPoints => _data.Tracks[0].Segments[0].Points;

    [Fact]
    [Trait("Req", "SIMP-04")]
    public void Simplify_shows_a_preview_and_applies_one_undoable_step()
    {
        SimplifyViewModel? shown = null;
        _dialogs.ToolDialog = vm =>
        {
            shown = (SimplifyViewModel)vm;
            return true;
        };

        _vm.SimplifyCommand.Execute(null);

        Assert.Equal("50 → 2 puntos (se eliminan 48) con tolerancia 5 m.", shown!.Preview);
        Assert.Equal(2, TrackPoints.Count);
        Assert.Equal("«Recta» simplificado: 50 → 2 puntos.", _vm.StatusMessage);
        Assert.True(_vm.ActiveDocument!.IsDirty);

        _vm.Undo();
        Assert.Equal(50, TrackPoints.Count);
    }

    [Fact]
    [Trait("Req", "NFR-02")]
    public async Task Large_tracks_are_previewed_in_the_background_and_only_the_last_tolerance_counts()
    {
        var layer = _vm.ActiveDocument!.SelectedLayer!;
        var dialog = new SimplifyViewModel(layer, backgroundThreshold: 10);
        Assert.True(dialog.IsComputing);
        Assert.False(dialog.CanApply);
        Assert.Equal("Calculando...", dialog.Preview);

        dialog.ToleranceText = "1000";
        await dialog.Computation;

        Assert.False(dialog.IsComputing);
        Assert.Equal(1000, dialog.Plan!.ToleranceMeters);
        Assert.True(dialog.CanApply);
        Assert.Equal(50, TrackPoints.Count); // the preview changes nothing

        dialog.ToleranceText = "7";
        dialog.Cancel();
        await dialog.Computation;

        Assert.False(dialog.IsComputing);
        Assert.Null(dialog.Plan);
        Assert.False(dialog.CanApply);
    }

    [Fact]
    [Trait("Req", "SIMP-04")]
    public void Cancelling_or_an_invalid_tolerance_changes_nothing()
    {
        _dialogs.ToolDialog = vm =>
        {
            var simplify = (SimplifyViewModel)vm;
            simplify.ToleranceText = "-3";
            Assert.False(simplify.CanApply);
            Assert.NotNull(simplify.Error);
            return true; // even if the dialog returned OK, there is no plan to apply
        };
        _vm.SimplifyCommand.Execute(null);

        _dialogs.ToolDialog = _ => false;
        _vm.SimplifyCommand.Execute(null);

        Assert.Equal(50, TrackPoints.Count);
        Assert.False(_vm.ActiveDocument!.IsDirty);
    }

    [Fact]
    [Trait("Req", "TIME-01")]
    [Trait("Req", "TIME-05")]
    [Trait("Req", "TIME-06")]
    public void Regenerate_times_previews_and_changes_only_the_selected_track()
    {
        TimesViewModel? shown = null;
        _dialogs.ToolDialog = vm =>
        {
            shown = (TimesViewModel)vm;
            shown.TimeZone = TimeZoneInfo.Utc;
            shown.StartDate = new DateTime(2026, 5, 1);
            shown.StartTimeText = "08:00";
            shown.SpeedText = "3,6";              // 1 m/s, decimal comma
            return true;
        };

        _vm.RegenerateTimesCommand.Execute(null);

        Assert.Contains("Inicio: 01/05/2026 08:00:00 (hora local) = 2026-05-01 08:00:00 UTC", shown!.Preview);
        Assert.Contains("Se sustituirán las horas que ya tenían 1 puntos.", shown.Preview);
        Assert.Equal(new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero), TrackPoints[0].Time);
        var meters = Geodesy.Distance(TrackPoints[0].Position, TrackPoints[^1].Position);
        Assert.Equal(meters, (TrackPoints[^1].Time!.Value - TrackPoints[0].Time!.Value).TotalSeconds, 2); // 1 m/s
        Assert.Equal(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), _data.Waypoints[0].Time); // untouched

        _vm.Undo();
        Assert.Equal(new DateTimeOffset(2026, 5, 1, 7, 0, 0, TimeSpan.Zero), TrackPoints[0].Time);
        Assert.Null(TrackPoints[1].Time);
    }

    [Fact]
    [Trait("Req", "TIME-01")]
    public void Times_dialog_starts_from_the_current_first_time_and_validates_input()
    {
        var times = new TimesViewModel(_vm.ActiveDocument!.SelectedLayer!) { TimeZone = TimeZoneInfo.Utc };
        Assert.Equal(new DateTime(2026, 5, 1), times.StartDate);

        times.StartTimeText = "07:00:00";
        Assert.True(times.CanApply);

        times.SpeedText = "0";
        Assert.False(times.CanApply);
        Assert.Equal("La velocidad debe ser un número mayor que 0.", times.Error);

        times.Uniform = true;
        times.IntervalText = "10";
        Assert.True(times.CanApply);
        Assert.Contains("Duración: 8 min 10 s · 50 puntos", times.Preview);

        times.StartTimeText = "25:00";
        Assert.Equal("Indica la fecha y la hora de inicio (HH:MM o HH:MM:SS).", times.Error);
    }

    [Fact]
    [Trait("Req", "TIME-05")]
    public void A_start_time_that_does_not_exist_is_rejected()
    {
        var times = new TimesViewModel(_vm.ActiveDocument!.SelectedLayer!)
        {
            TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time"),
            StartDate = new DateTime(2026, 3, 29),
            StartTimeText = "02:30",
        };

        Assert.False(times.CanApply);
        Assert.Equal("Esa hora no existe en la zona elegida por el cambio de horario de verano.", times.Error);
    }

    [Fact]
    [Trait("Req", "TIME-06")]
    [Trait("Req", "SIMP-04")]
    public void Tools_are_only_available_for_tracks_and_routes()
    {
        Assert.True(_vm.SimplifyCommand.CanExecute(null));
        _vm.ActiveDocument!.SelectedLayer = _vm.ActiveDocument.Layers.Single(l => l.Kind == LayerKind.Waypoints);

        Assert.False(_vm.SimplifyCommand.CanExecute(null));
        Assert.False(_vm.RegenerateTimesCommand.CanExecute(null));
        Assert.False(_vm.ReverseCommand.CanExecute(null));
    }

    [Fact]
    [Trait("Req", "REV-03")]
    public void Reverse_applies_one_undoable_step_to_the_selected_track_only()
    {
        var (first, last) = (TrackPoints[0], TrackPoints[^1]);
        var waypointTime = _data.Waypoints[0].Time;

        _vm.ReverseCommand.Execute(null);

        Assert.Same(last, TrackPoints[0]);
        Assert.Same(first, TrackPoints[^1]);
        Assert.Equal(new DateTimeOffset(2026, 5, 1, 7, 0, 0, TimeSpan.Zero), first.Time); // single time: mirrored onto itself
        Assert.Equal(waypointTime, _data.Waypoints[0].Time);
        Assert.Equal("«Recta» invertido: 50 puntos.", _vm.StatusMessage);
        Assert.True(_vm.ActiveDocument!.IsDirty);

        _vm.Undo();
        Assert.Same(first, TrackPoints[0]);
        Assert.False(_vm.ActiveDocument.IsDirty);
    }
}
