using Mendikat.TrailLab.App.Charting;
using Mendikat.TrailLab.App.ViewModels;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Model;
using Mendikat.TrailLab.Tests.Domain;
using Mendikat.TrailLab.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mendikat.TrailLab.Tests.App;

public sealed class ProfileTests
{
    private const string Path = @"C:\data\perfil.gpx";

    private readonly FakeDialogService _dialogs = new();
    private readonly FakeDocumentStore _store = new();
    private readonly MainViewModel _vm;
    private readonly Track _track = new() { Name = "Subida" };
    private readonly TrailData _data = new();

    public ProfileTests()
    {
        var segment = new TrackSegment();
        segment.Points.AddRange(EditingTests.Points(10));
        _track.Segments.Add(segment);
        _data.Tracks.Add(_track);
        _data.Waypoints.AddRange(EditingTests.Points(2));
        _store.Contents[Path] = _data;
        _vm = new MainViewModel(_dialogs, _store, new FakeSettingsStore(), NullLogger<MainViewModel>.Instance);
        _dialogs.OpenPaths.Enqueue(Path);
        _vm.OpenAsync(DocumentFormat.Gpx).GetAwaiter().GetResult();
    }

    private DocumentViewModel Doc => _vm.ActiveDocument!;

    private List<TrailPoint> Points => _track.Segments[0].Points;

    private ProfileViewModel OpenProfile()
    {
        ProfileViewModel? profile = null;
        _vm.ProfileRequested += (_, p) => profile = p;
        _vm.ElevationProfileCommand.Execute(null);
        return profile ?? throw new InvalidOperationException("Profile not requested");
    }

    [Fact]
    [Trait("Req", "APP-06")]
    [Trait("Req", "PROF-01")]
    public void Profile_button_is_enabled_only_for_tracks_and_routes()
    {
        Assert.True(_vm.ElevationProfileCommand.CanExecute(null));

        Doc.SelectedLayer = Doc.Layers.Single(l => l.Kind == LayerKind.Waypoints);
        Assert.False(_vm.ElevationProfileCommand.CanExecute(null));
    }

    [Fact]
    [Trait("Req", "PROF-01")]
    public void Profile_window_is_titled_with_the_track_and_shows_a_summary()
    {
        var profile = OpenProfile();

        Assert.Equal("Perfil de elevación — Subida (perfil.gpx)", profile.Title);
        Assert.Equal(10, profile.Profile.Samples.Count);
        Assert.StartsWith("Distancia: 1,00 km · Elevación mínima: 500 m · máxima: 590 m", profile.Summary);
        Assert.False(profile.HasMissingWarning);
    }

    [Fact]
    [Trait("Req", "PROF-04")]
    public void Hovering_the_profile_reports_the_point_and_clicking_selects_it()
    {
        var profile = OpenProfile();
        TrailPoint? hovered = null;
        profile.HoveredPointChanged += (_, p) => hovered = p;
        var distance = profile.Profile.Samples[3].Distance;

        profile.Hover(distance + 1);
        Assert.Same(Points[3], hovered);
        Assert.StartsWith("km 0,333 · 530,0 m · punto 4", profile.HoverText);

        profile.SelectAt(distance);
        Assert.Same(Points[3], Assert.Single(Doc.SelectedRows).Point);
        Assert.Equal(distance, profile.CursorDistance);

        profile.Hover(null);
        Assert.Null(hovered);
        Assert.Null(profile.HoverDistance);
    }

    [Fact]
    [Trait("Req", "PROF-04")]
    public void Selecting_a_point_in_the_list_or_map_moves_the_profile_cursor()
    {
        var profile = OpenProfile();

        _vm.SelectFromMap(Doc, Points[7]);

        Assert.Equal(profile.Profile.Samples[7].Distance, profile.CursorDistance);

        _vm.SelectFromMap(Doc, _data.Waypoints[0]); // a point outside this profile
        Assert.Null(profile.CursorDistance);
    }

    [Fact]
    [Trait("Req", "PROF-04")]
    public void Arrow_keys_step_through_the_points()
    {
        var profile = OpenProfile();

        profile.Step(+1);
        Assert.Same(Points[0], Doc.SelectedRows.Single().Point);
        profile.Step(+1);
        profile.Step(+1);
        Assert.Same(Points[2], Doc.SelectedRows.Single().Point);
        profile.Step(-1);
        Assert.Same(Points[1], Doc.SelectedRows.Single().Point);
    }

    [Fact]
    [Trait("Req", "EDIT-07")]
    [Trait("Req", "PROF-03")]
    public void Edits_update_the_profile_and_deleting_the_track_closes_it()
    {
        var profile = OpenProfile();
        var closed = false;
        profile.CloseRequested += (_, _) => closed = true;

        _vm.SelectFromMap(Doc, Points[0]);
        Doc.Detail!.Elevation = "";
        _vm.ApplyPointDetail();
        Assert.Equal(1, profile.Profile.MissingElevations);
        Assert.StartsWith("⚠ 1 de 10 puntos no tienen elevación", profile.MissingWarning);

        Doc.SetSelectedRows([]); // no point selected: Supr deletes the whole track (confirmed by the fake dialog)
        _vm.DeleteSelection();
        Assert.True(closed);
    }

    [Theory]
    [Trait("Req", "PROF-01")]
    [InlineData(4420, 5, 1000)]
    [InlineData(4420, 10, 500)]
    [InlineData(134, 5, 50)]
    [InlineData(0.9, 4, 0.5)]
    public void Axis_steps_are_1_2_or_5_times_a_power_of_ten(double range, int maxTicks, double expected)
    {
        Assert.Equal(expected, AxisTicks.NiceStep(range, maxTicks), 9);
    }

    [Fact]
    [Trait("Req", "PROF-01")]
    public void Axis_ticks_cover_the_range_with_round_values()
    {
        Assert.Equal([600, 650, 700], AxisTicks.Ticks(576, 724, 4));
    }
}
