using System.Globalization;
using Mendikat.TrailLab.App.Resources;
using Mendikat.TrailLab.Domain.Elevation;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Domain.Model;
using Mendikat.TrailLab.Mapping.Elevation;
using Microsoft.Extensions.Logging;

namespace Mendikat.TrailLab.App.ViewModels;

/// <summary>
/// Datum and elevation under the pointer in the status bar (COORD-07, COORD-08). The elevation is looked up when
/// the pointer stops: that of the file's point under it if it has one, otherwise the terrain from the IGN DEM.
/// </summary>
public sealed partial class MainViewModel
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>After a failure of the elevation service, the terrain is not queried again for this long.</summary>
    private static readonly TimeSpan TerrainRetryDelay = TimeSpan.FromMinutes(1);

    private string _pointerElevation = Strings.StatusPointerOutside;
    private string? _pointerElevationToolTip;
    private CancellationTokenSource? _elevationLookup;
    private DateTime _terrainPausedUntil = DateTime.MinValue;

    /// <summary>Datum of the active document's coordinates; WGS84 for the map when no document is open (COORD-07).</summary>
    public string PointerDatum => ActiveDocument?.Document.Data.Datum ?? TrailData.Wgs84;

    /// <summary>"Alt. 1.234 m (archivo)", "Alt. 1.234 m (MDT IGN)" or "—" (COORD-08).</summary>
    public string PointerElevation
    {
        get => _pointerElevation;
        private set => SetProperty(ref _pointerElevation, value);
    }

    /// <summary>Where the elevation comes from (point of the file, or DEM name, vertical datum and attribution).</summary>
    public string? PointerElevationToolTip
    {
        get => _pointerElevationToolTip;
        private set => SetProperty(ref _pointerElevationToolTip, value);
    }

    /// <summary>The visible point of an open document under a position, or null (set by the map view: LAY-03 hit test).</summary>
    public Func<GeoPosition, TrailPoint?>? PointAt { get; set; }

    /// <summary>How long the pointer must stay still before the elevation is looked up.</summary>
    public TimeSpan ElevationDelay { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>The last elevation lookup (for tests); completes when it has finished or been superseded.</summary>
    public Task PointerElevationLookup { get; private set; } = Task.CompletedTask;

    private void ScheduleElevationLookup(GeoPosition? position)
    {
        _elevationLookup?.Cancel();
        _elevationLookup = null;
        PointerElevation = Strings.StatusPointerOutside;
        PointerElevationToolTip = null;
        if (position is not { } p)
        {
            PointerElevationLookup = Task.CompletedTask;
            return;
        }

        _elevationLookup = new CancellationTokenSource();
        PointerElevationLookup = LookupElevationAsync(p, _elevationLookup.Token);
    }

    private async Task LookupElevationAsync(GeoPosition position, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(ElevationDelay, cancellationToken);
            if (PointAt?.Invoke(position) is { Elevation: { } elevation })
            {
                ShowElevation(elevation, Strings.ElevationSourceFile, Strings.ElevationFileToolTip);
                return;
            }

            if (ElevationSources is null || DateTime.UtcNow < _terrainPausedUntil)
            {
                return;
            }

            var source = ElevationSources(IgnDemResolution.FiveMeters);
            var values = await source.GetElevationsAsync([position], null, cancellationToken);
            if (!cancellationToken.IsCancellationRequested && values[0] is { } terrain)
            {
                ShowElevation(terrain, source.Name, $"{source.Name}. {source.VerticalDatum}{Environment.NewLine}{source.Attribution}");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The pointer moved on: a newer lookup replaces this one.
        }
        catch (ElevationSourceException ex)
        {
            // Offline or service error: the status bar simply shows no terrain elevation for a while (NFR-05).
            _terrainPausedUntil = DateTime.UtcNow + TerrainRetryDelay;
            _logger.LogInformation("Pointer elevation unavailable: {Reason}", ex.Message);
        }
    }

    private void ShowElevation(double meters, string source, string toolTip)
    {
        PointerElevation = string.Format(Spanish, Strings.StatusElevation, meters.ToString("N0", Spanish), source);
        PointerElevationToolTip = toolTip;
    }
}
