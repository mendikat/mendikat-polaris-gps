using System.Globalization;
using Polaris.Gps.App.Resources;
using Polaris.Gps.Domain.Elevation;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Mapping.Elevation;

namespace Polaris.Gps.App.ViewModels;

/// <summary>
/// "Adjust elevations with DEM" dialog (DEM-01..05): queries the IGN terrain model for the points of a track or
/// route, with progress and cancel, then previews how many points change and how many have no data. Nothing changes
/// until the user applies; the result is one undoable step.
/// </summary>
public sealed class DemViewModel : ObservableObject
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private readonly Func<IgnDemResolution, IElevationSource> _sources;
    private readonly IReadOnlyList<TrailPoint> _points;
    private DemResolutionOption _resolution;
    private ElevationPlan? _plan;
    private string? _error;
    private bool _isQuerying;
    private double _progress;
    private CancellationTokenSource? _cancellation;

    public DemViewModel(LayerItemViewModel layer, Func<IgnDemResolution, IElevationSource> sources, IEnumerable<IElevationSource>? localSources = null)
    {
        Layer = layer;
        _sources = sources;
        _points = layer.PointLists.SelectMany(l => l.Points).ToList();
        Resolutions =
        [
            new(IgnDemResolution.FiveMeters, Strings.DemResolution5),
            new(IgnDemResolution.TwentyFiveMeters, Strings.DemResolution25),
        ];
        // Local DEMs loaded with File › Open cartography (LDEM-01) are offered after the IGN ones.
        Resolutions = [.. Resolutions, .. (localSources ?? []).Select(s => new DemResolutionOption(IgnDemResolution.FiveMeters, s.Name, s))];
        _resolution = Resolutions[0];
        QueryCommand = new RelayCommand(_ => QueryAsync(), _ => !IsQuerying && _points.Count > 0);
        CancelQueryCommand = new RelayCommand(CancelQuery, () => IsQuerying);
    }

    public LayerItemViewModel Layer { get; }

    public string Title => string.Format(Strings.DemTitle, Layer.Name);

    public IReadOnlyList<DemResolutionOption> Resolutions { get; }

    /// <summary>Changing the resolution discards the previous result (it must be queried again).</summary>
    public DemResolutionOption Resolution
    {
        get => _resolution;
        set
        {
            if (SetProperty(ref _resolution, value ?? Resolutions[0]))
            {
                Plan = null;
                OnPropertyChanged(nameof(SourceInfo));
            }
        }
    }

    public string SourceInfo
    {
        get
        {
            var source = Source(Resolution);
            return $"{source.Attribution}{Environment.NewLine}{Strings.DemDatum} {source.VerticalDatum}";
        }
    }

    public RelayCommand QueryCommand { get; }

    public RelayCommand CancelQueryCommand { get; }

    public bool IsQuerying
    {
        get => _isQuerying;
        private set
        {
            if (SetProperty(ref _isQuerying, value))
            {
                QueryCommand.RaiseCanExecuteChanged();
                CancelQueryCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(CanApply));
                OnPropertyChanged(nameof(CanChangeResolution));
            }
        }
    }

    public bool CanChangeResolution => !IsQuerying;

    /// <summary>Download progress, 0 to 1.</summary>
    public double Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
    }

    public ElevationPlan? Plan
    {
        get => _plan;
        private set
        {
            if (SetProperty(ref _plan, value))
            {
                OnPropertyChanged(nameof(Preview));
                OnPropertyChanged(nameof(CanApply));
            }
        }
    }

    public string? Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    public bool CanApply => !IsQuerying && Plan?.Command is not null;

    /// <summary>Counts of changed / unchanged / no-data points and the elevation differences (DEM-02).</summary>
    public string Preview
    {
        get
        {
            if (Plan is not { } plan)
            {
                return string.Empty;
            }

            var lines = new List<string>
            {
                string.Format(Spanish, Strings.DemPreviewCounts, N(plan.Total), N(plan.Changed), N(plan.Unchanged), N(plan.NoData)),
            };
            if (plan.PreviouslyMissing > 0)
            {
                lines.Add(string.Format(Spanish, Strings.DemPreviewMissing, N(plan.PreviouslyMissing)));
            }

            if (plan.MinDifference is { } min && plan.MaxDifference is { } max && plan.MeanDifference is { } mean)
            {
                lines.Add(string.Format(Spanish, Strings.DemPreviewDifferences, Signed(min), Signed(max), Signed(mean)));
            }

            if (plan.Command is null)
            {
                lines.Add(Strings.DemPreviewNothing);
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>Downloads the DEM values and builds the plan. Errors are shown in the dialog; nothing is changed.</summary>
    public async Task QueryAsync()
    {
        _cancellation = new CancellationTokenSource();
        IsQuerying = true;
        Error = null;
        Plan = null;
        Progress = 0;
        try
        {
            var source = Source(Resolution);
            var progress = new Progress<double>(p => Progress = p);
            var values = await source.GetElevationsAsync(_points.Select(p => p.Position).ToList(), progress, _cancellation.Token);
            Plan = ElevationAdjustment.Plan(_points, values);
        }
        catch (OperationCanceledException)
        {
            Error = Strings.DemCancelled;
        }
        catch (ElevationSourceException)
        {
            Error = Strings.DemServiceError;
        }
        finally
        {
            IsQuerying = false;
            _cancellation.Dispose();
            _cancellation = null;
        }
    }

    /// <summary>Stops the download (also called when the dialog is closed while querying).</summary>
    public void CancelQuery() => _cancellation?.Cancel();

    private IElevationSource Source(DemResolutionOption option) => option.Local ?? _sources(option.Resolution);

    private static string N(int value) => value.ToString("N0", Spanish);

    private static string Signed(double meters) => meters.ToString("+0.0;−0.0;0.0", Spanish);
}

/// <summary>An IGN DEM resolution, or a local DEM (<paramref name="Local"/>), with its display name.</summary>
public sealed record DemResolutionOption(IgnDemResolution Resolution, string DisplayName, IElevationSource? Local = null)
{
    public override string ToString() => DisplayName;
}
