using Polaris.Gps.App.Resources;
using Polaris.Gps.Domain.Editing;
using Polaris.Gps.Domain.Elevation;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Domain.Processing;
using Polaris.Gps.Domain.Profiles;
using Polaris.Gps.Mapping.Elevation;
using Polaris.Gps.Mapping.Routing;
using Microsoft.Extensions.Logging;

namespace Polaris.Gps.App.ViewModels;

/// <summary>A routing profile as shown in the list ("Senderismo de montaña").</summary>
public sealed record RoutingProfileOption(RoutingProfile Profile, string Name)
{
    public override string ToString() => Name;
}

/// <summary>
/// Drawing on the map: the drawing mode (a click adds a point, Esc ends it), following paths with BRouter, elevation
/// of the new points from the DEMs, snapping to existing points, live figures of what is drawn (distance, ascent and
/// MIDE time), closing a loop, out and back, and new tracks or routes in the open document. Every change is undoable.
/// </summary>
public sealed partial class MainViewModel
{
    private bool _isDrawing;
    private bool _followPaths;
    private bool _snapToPoints = true;
    private bool _autoElevation = true;
    private bool _isRouting;
    private RoutingProfileOption _routingProfile = null!;
    private string? _drawingStats;

    /// <summary>Finds the way along paths for "Follow paths". Set at startup; without it the option is disabled.</summary>
    public IRouter? Router { get; init; }

    public RelayCommand ToggleDrawingCommand { get; private set; } = null!;

    public RelayCommand NewTrackCommand { get; private set; } = null!;

    public RelayCommand NewRouteCommand { get; private set; } = null!;

    public RelayCommand CloseLoopCommand { get; private set; } = null!;

    public RelayCommand OutAndBackCommand { get; private set; } = null!;

    public IReadOnlyList<RoutingProfileOption> RoutingProfiles { get; private set; } = [];

    /// <summary>Drawing mode: a click on the map adds a point (as Ctrl+click); dragging still moves the map. Esc ends it.</summary>
    public bool IsDrawing
    {
        get => _isDrawing;
        set
        {
            if (value && ActiveDocument is null)
            {
                NewGpx();
            }

            if (SetProperty(ref _isDrawing, value))
            {
                StatusMessage = value ? Strings.StatusDrawingOn : Strings.StatusDrawingOff;
                CancelOperationCommand.RaiseCanExecuteChanged();
                UpdateDrawingStats();
            }
        }
    }

    /// <summary>Each new point is joined to the previous one along paths and roads (BRouter) instead of a straight line.</summary>
    public bool FollowPaths
    {
        get => _followPaths;
        set
        {
            if (SetProperty(ref _followPaths, value))
            {
                SaveDrawingSettings();
            }
        }
    }

    public bool CanFollowPaths => Router is not null;

    public RoutingProfileOption RoutingProfile
    {
        get => _routingProfile;
        set
        {
            if (value is not null && SetProperty(ref _routingProfile, value))
            {
                SaveDrawingSettings();
            }
        }
    }

    /// <summary>A click near an existing point (of any open document) or waypoint takes its exact position and elevation.</summary>
    public bool SnapToPoints
    {
        get => _snapToPoints;
        set
        {
            if (SetProperty(ref _snapToPoints, value))
            {
                SaveDrawingSettings();
            }
        }
    }

    /// <summary>New points take the terrain elevation: user DEMs first, then the IGN DEM (5 m, Spain, online).</summary>
    public bool AutoElevation
    {
        get => _autoElevation;
        set
        {
            if (SetProperty(ref _autoElevation, value))
            {
                SaveDrawingSettings();
            }
        }
    }

    /// <summary>A way is being computed; clicks are ignored meanwhile.</summary>
    public bool IsRouting
    {
        get => _isRouting;
        private set => SetProperty(ref _isRouting, value);
    }

    /// <summary>"3,25 km · +450 m / −120 m · ≈ 1 h 35 min" of the selected track or route while drawing; null otherwise.</summary>
    public string? DrawingStats
    {
        get => _drawingStats;
        private set => SetProperty(ref _drawingStats, value);
    }

    /// <summary>
    /// Ctrl+click on the map, or a click in drawing mode (APP-08): adds a point at <paramref name="position"/> to the
    /// active document, right after the selected point, or at the end of the selected track, route or waypoint list.
    /// The new point is selected, so consecutive clicks draw a line. Undoable like any edit.
    /// </summary>
    public void AddPointAt(GeoPosition position) => _ = AddPointAtAsync(position);

    /// <summary>
    /// As <see cref="AddPointAt"/>, completing when the point (or the stretch along the path) has been added. Near an
    /// existing point it snaps to it; with "Follow paths" the stretch from the previous point is routed; new points take
    /// the terrain elevation. Finishes at once (synchronously) when nothing has to be looked up.
    /// </summary>
    public async Task AddPointAtAsync(GeoPosition position)
    {
        var doc = ActiveDocument;
        if (IsBusy || IsRouting || doc is null)
        {
            return;
        }

        try
        {
            var after = doc.SelectedRows.Count == 1 ? doc.SelectedRows[0].Point : null;
            var target = doc.SelectedLayer?.Model;
            var snapped = SnapToPoints ? PointAt?.Invoke(position) : null;
            if (snapped is not null)
            {
                if (ReferenceEquals(snapped, after))
                {
                    return; // a click on the last point drawn adds nothing
                }

                position = snapped.Position;
            }

            List<TrailPoint> points;
            var isSequence = doc.SelectedLayer is null || doc.SelectedLayer.IsSequence;
            if (FollowPaths && Router is not null && after is not null && isSequence)
            {
                if (await RouteAsync(after.Position, position) is not { } way)
                {
                    return;
                }

                points = way;
            }
            else
            {
                points = [new TrailPoint(position)
                {
                    IsAdded = true,
                    Elevation = snapped?.Elevation,
                    ElevationSource = snapped?.Elevation is null ? ElevationSource.Original : snapped.ElevationSource,
                }];
            }

            if (AutoElevation && snapped is null)
            {
                await FillElevationsAsync(points);
            }

            doc.Document.Execute(PointEditing.CreateAdditions(doc.Document.Data, target, after, points));
            doc.SelectPoints([points[^1]]);
            StatusMessage = points.Count > 1
                ? string.Format(Strings.StatusRouted, points.Count, Distance(points, after))
                : snapped is not null ? Strings.StatusPointSnapped : IsDrawing ? Strings.StatusPointAddedDrawing : Strings.StatusPointAdded;
            _logger.LogInformation("Added {Count} points in {FileName}", points.Count, doc.FileName);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Fire and forget from the map: report instead of losing the error.
            _logger.LogError(ex, "Adding a point failed");
            StatusMessage = string.Format(Strings.ErrorUnexpected, ex.Message);
        }
    }

    /// <summary>Ends the selected track or route where it started (along paths with "Follow paths").</summary>
    public async Task CloseLoopAsync()
    {
        if (ActiveDocument is not { SelectedLayer: { IsSequence: true } layer } doc || IsRouting)
        {
            StatusMessage = Strings.StatusSelectSomething;
            return;
        }

        var (first, last) = Ends(layer.Model);
        if (first is null || last is null)
        {
            StatusMessage = Strings.StatusSelectSomething;
            return;
        }

        List<TrailPoint>? way = null;
        if (FollowPaths && Router is not null && last.Position != first.Position)
        {
            way = await RouteAsync(last.Position, first.Position);
            if (way is null)
            {
                return;
            }

            if (AutoElevation)
            {
                await FillElevationsAsync(way);
            }
        }

        if (PointEditing.CloseLoop(layer.Model, way) is not { } command)
        {
            StatusMessage = Strings.StatusAlreadyClosed;
            return;
        }

        doc.Document.Execute(command);
        StatusMessage = string.Format(Strings.StatusLoopClosed, layer.Name);
    }

    /// <summary>Adds the way back along the same points to the selected track or route.</summary>
    public void OutAndBack()
    {
        if (ActiveDocument is not { SelectedLayer: { IsSequence: true } layer } doc || PointEditing.OutAndBack(layer.Model) is not { } command)
        {
            StatusMessage = Strings.StatusSelectSomething;
            return;
        }

        doc.Document.Execute(command);
        StatusMessage = string.Format(Strings.StatusOutAndBack, layer.Name);
    }

    /// <summary>Edit › New track: an empty track in the active document, selected, in drawing mode.</summary>
    public void NewTrack() => NewItem(isTrack: true);

    /// <summary>Edit › New route: an empty route in the active document, selected, in drawing mode.</summary>
    public void NewRoute() => NewItem(isTrack: false);

    private void NewItem(bool isTrack)
    {
        if (ActiveDocument is null)
        {
            NewGpx();
            if (isTrack)
            {
                IsDrawing = true;
                return; // the new document already has an empty track
            }
        }

        var doc = ActiveDocument!;
        var data = doc.Document.Data;
        object item;
        if (isTrack)
        {
            var track = new Track { Name = string.Format(Strings.NewTrackNumbered, data.Tracks.Count + 1) };
            track.Segments.Add(new TrackSegment());
            doc.Document.Execute(new AddItemCommand<Track>(data.Tracks, track));
            item = track;
        }
        else
        {
            var route = new Route { Name = string.Format(Strings.NewRouteNumbered, data.Routes.Count + 1) };
            doc.Document.Execute(new AddItemCommand<Route>(data.Routes, route));
            item = route;
        }

        doc.SelectedLayer = doc.Layers.FirstOrDefault(l => ReferenceEquals(l.Model, item));
        IsDrawing = true;
    }

    /// <summary>The routed points from <paramref name="from"/> to <paramref name="to"/> without the first one, or null (status explains why).</summary>
    private async Task<List<TrailPoint>?> RouteAsync(GeoPosition from, GeoPosition to)
    {
        IsRouting = true;
        StatusMessage = Strings.StatusRouting;
        try
        {
            var way = await Router!.RouteAsync(from, to, RoutingProfile.Profile.Id, CancellationToken.None);
            return way.Skip(1).Select(p => new TrailPoint(p.Position)
            {
                IsAdded = true,
                Elevation = p.Elevation,
                ElevationSource = p.Elevation is null ? ElevationSource.Original : ElevationSource.Dem,
            }).ToList();
        }
        catch (RoutingException ex)
        {
            StatusMessage = ex.NoRoute ? Strings.StatusNoRoute : Strings.StatusRoutingFailed;
            _logger.LogInformation("Routing failed ({Kind}): {Reason}", ex.NoRoute ? "no route" : "service", ex.Message);
            return null;
        }
        finally
        {
            IsRouting = false;
        }
    }

    /// <summary>
    /// Terrain elevation for new points: user DEMs first (offline), then the IGN 5 m DEM for the rest, which replaces the
    /// coarser elevation given by the router. Points without data keep what they had. Never fails: offline, nothing changes.
    /// </summary>
    private async Task FillElevationsAsync(List<TrailPoint> points)
    {
        var pending = new List<TrailPoint>();
        foreach (var point in points)
        {
            var local = LocalDems.Select(d => d.ElevationAt(point.Position)).FirstOrDefault(e => e is not null);
            if (local is { } elevation)
            {
                point.Elevation = elevation;
                point.ElevationSource = ElevationSource.Dem;
            }
            else
            {
                pending.Add(point);
            }
        }

        if (pending.Count == 0 || ElevationSources is null || DateTime.UtcNow < _terrainPausedUntil)
        {
            return;
        }

        try
        {
            var values = await ElevationSources(IgnDemResolution.FiveMeters).GetElevationsAsync(pending.Select(p => p.Position).ToList(), null, CancellationToken.None);
            for (var i = 0; i < pending.Count; i++)
            {
                if (values[i] is { } elevation)
                {
                    pending[i].Elevation = Math.Round(elevation, 1);
                    pending[i].ElevationSource = ElevationSource.Dem;
                }
            }
        }
        catch (ElevationSourceException ex)
        {
            _terrainPausedUntil = DateTime.UtcNow + TerrainRetryDelay;
            _logger.LogInformation("Elevation of the drawn points unavailable: {Reason}", ex.Message);
        }
    }

    private static (TrailPoint? First, TrailPoint? Last) Ends(object item) => item switch
    {
        Track track => (track.AllPoints.FirstOrDefault(), track.AllPoints.LastOrDefault()),
        Route route => (route.Points.FirstOrDefault(), route.Points.LastOrDefault()),
        _ => (null, null),
    };

    /// <summary>"1,2 km": length of the stretch drawn from <paramref name="after"/>.</summary>
    private static string Distance(List<TrailPoint> points, TrailPoint? after)
    {
        var meters = Geodesy.Length(after is null ? points : [after, .. points]);
        return $"{(meters / 1000).ToString("N2", Spanish)} km";
    }

    /// <summary>Figures of the selected track or route while drawing: distance, ascent and descent, and MIDE time.</summary>
    private void UpdateDrawingStats()
    {
        if (!IsDrawing || ActiveDocument?.SelectedLayer is not { IsSequence: true } layer)
        {
            DrawingStats = null;
            return;
        }

        var lists = layer.PointLists.Select(l => (IReadOnlyList<TrailPoint>)l.Points).ToList();
        var profile = ElevationProfile.Build(lists);
        var seconds = MideTime.CumulativeSeconds(lists);
        var time = seconds.Length > 0 ? ProfileViewModel.Duration(TimeSpan.FromSeconds(seconds[^1])) : "—";
        DrawingStats = string.Format(
            Spanish,
            Strings.DrawingStats,
            (profile.TotalDistance / 1000).ToString("N2", Spanish),
            profile.Ascent.ToString("N0", Spanish),
            profile.Descent.ToString("N0", Spanish),
            time);
    }

    private void SaveDrawingSettings()
    {
        var current = _settings.Load();
        var drawing = current.Drawing with
        {
            FollowPaths = FollowPaths,
            RoutingProfile = RoutingProfile.Profile.Id,
            SnapToPoints = SnapToPoints,
            AutoElevation = AutoElevation,
        };
        if (drawing != current.Drawing && !_settings.Save(current with { Drawing = drawing }))
        {
            StatusMessage = Strings.StatusMapSaveSettingsFailed;
        }
    }

    private void InitializeDrawing(Settings.DrawingSettings saved)
    {
        RoutingProfiles = BRouterClient.Profiles.Select(p => new RoutingProfileOption(p, p.Activity switch
        {
            RoutingActivity.MountainHiking => Strings.RoutingHiking,
            RoutingActivity.Trekking => Strings.RoutingTrekking,
            RoutingActivity.MountainBike => Strings.RoutingMtb,
            RoutingActivity.RoadBike => Strings.RoutingRoadBike,
            _ => Strings.RoutingShortest,
        })).ToList();
        _routingProfile = RoutingProfiles.FirstOrDefault(p => p.Profile.Id == saved.RoutingProfile) ?? RoutingProfiles[0];
        _followPaths = saved.FollowPaths;
        _snapToPoints = saved.SnapToPoints;
        _autoElevation = saved.AutoElevation;

        ToggleDrawingCommand = new RelayCommand(() => IsDrawing = !IsDrawing, () => !IsBusy);
        NewTrackCommand = new RelayCommand(NewTrack, () => !IsBusy);
        NewRouteCommand = new RelayCommand(NewRoute, () => !IsBusy);
        CloseLoopCommand = new RelayCommand(_ => CloseLoopAsync(), _ => CanUseTools());
        OutAndBackCommand = new RelayCommand(OutAndBack, CanUseTools);
    }

    private void RefreshDrawingCommands()
    {
        ToggleDrawingCommand.RaiseCanExecuteChanged();
        NewTrackCommand.RaiseCanExecuteChanged();
        NewRouteCommand.RaiseCanExecuteChanged();
        CloseLoopCommand.RaiseCanExecuteChanged();
        OutAndBackCommand.RaiseCanExecuteChanged();
    }
}
