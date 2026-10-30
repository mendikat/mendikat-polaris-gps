using System.Globalization;
using Polaris.Gps.App.Resources;
using Polaris.Gps.App.Services;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Domain.Processing;
using Polaris.Gps.Domain.Profiles;

namespace Polaris.Gps.App.ViewModels;

/// <summary>
/// "Clean track" dialog: GPS jumps and spikes, stops and elevation smoothing, each with its limit, and a live preview.
/// Nothing changes until the user applies; the result is one undoable step. Large tracks are computed in the
/// background once typing pauses, so the dialog never freezes.
/// </summary>
public sealed class CleanViewModel : ObservableObject
{
    /// <summary>Above this many points the preview is computed in the background.</summary>
    public const int DefaultBackgroundThreshold = 20_000;

    private static readonly TimeSpan TypingPause = TimeSpan.FromMilliseconds(300);
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    private readonly IReadOnlyList<List<TrailPoint>> _lists;
    private readonly bool _inBackground;
    private bool _removeJumps;
    private string _maxSpeedText = TrackCleaner.DefaultMaxSpeedKmh.ToString(Spanish);
    private bool _removeSpikes = true;
    private string _spikeText = TrackCleaner.DefaultSpikeMeters.ToString(Spanish);
    private bool _removeStops;
    private string _stopRadiusText = TrackCleaner.DefaultStopRadiusMeters.ToString(Spanish);
    private bool _smoothElevation;
    private string _smoothWindowText = ProfileStatistics.SmoothingWindow.ToString(Spanish);
    private CleaningPlan? _plan;
    private string? _error;
    private bool _isComputing;
    private CancellationTokenSource? _computation;

    public CleanViewModel(LayerItemViewModel layer, int backgroundThreshold = DefaultBackgroundThreshold)
    {
        Layer = layer;
        _lists = layer.PointLists.Select(l => l.Points).ToList();
        var points = _lists.SelectMany(l => l).ToList();
        HasTimes = points.Count(p => p.Time is not null) >= 2;
        HasElevations = points.Count(p => p.Elevation is not null) >= 2;
        _removeJumps = HasTimes;
        _removeStops = HasTimes;
        _inBackground = points.Count > backgroundThreshold;
        Recompute();
    }

    public LayerItemViewModel Layer { get; }

    public string Title => string.Format(Strings.CleanTitle, Layer.Name);

    /// <summary>Jumps by speed and stops only make sense on recorded tracks, with times.</summary>
    public bool HasTimes { get; }

    public bool HasElevations { get; }

    public bool RemoveJumps { get => _removeJumps; set => SetOption(ref _removeJumps, value); }

    public string MaxSpeedText { get => _maxSpeedText; set => SetOption(ref _maxSpeedText, value ?? string.Empty); }

    public bool RemoveSpikes { get => _removeSpikes; set => SetOption(ref _removeSpikes, value); }

    public string SpikeText { get => _spikeText; set => SetOption(ref _spikeText, value ?? string.Empty); }

    public bool RemoveStops { get => _removeStops; set => SetOption(ref _removeStops, value); }

    public string StopRadiusText { get => _stopRadiusText; set => SetOption(ref _stopRadiusText, value ?? string.Empty); }

    public bool SmoothElevation { get => _smoothElevation; set => SetOption(ref _smoothElevation, value); }

    /// <summary>Width in meters of the Gaussian smoothing window; 100 m, as the ascent of mendikat.net, by default.</summary>
    public string SmoothWindowText { get => _smoothWindowText; set => SetOption(ref _smoothWindowText, value ?? string.Empty); }

    public CleaningPlan? Plan
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

    public bool IsComputing
    {
        get => _isComputing;
        private set
        {
            if (SetProperty(ref _isComputing, value))
            {
                OnPropertyChanged(nameof(Preview));
                OnPropertyChanged(nameof(CanApply));
            }
        }
    }

    /// <summary>The background computation in progress (completed when there is none); lets tests wait for it.</summary>
    public Task Computation { get; private set; } = Task.CompletedTask;

    /// <summary>"Se eliminan 12 saltos y 340 puntos de paradas; se suavizan 1.205 altitudes. 5.432 → 5.080 puntos."</summary>
    public string Preview => IsComputing
        ? Strings.SimplifyComputing
        : Plan is { } plan
            ? string.Format(Spanish, Strings.CleanPreview, N(plan.Jumps), N(plan.StopPoints), N(plan.Smoothed), N(plan.Total), N(plan.Total - plan.Removed))
            : string.Empty;

    public bool CanApply => !IsComputing && Plan?.Command is not null;

    public void Cancel()
    {
        _computation?.Cancel();
        _computation = null;
        IsComputing = false;
    }

    private static string N(int value) => value.ToString("N0", Spanish);

    private void SetOption<T>(ref T field, T value)
    {
        if (SetProperty(ref field, value))
        {
            Recompute();
        }
    }

    private void Recompute()
    {
        Cancel();
        if (BuildOptions() is not { } options)
        {
            Plan = null;
            return;
        }

        Error = null;
        if (!_inBackground)
        {
            Plan = TrackCleaner.Plan(_lists, options);
            return;
        }

        Plan = null;
        IsComputing = true;
        _computation = new CancellationTokenSource();
        Computation = ComputeAsync(options, _computation);
    }

    /// <summary>The options from the texts; null (with <see cref="Error"/>) if a limit of a chosen step is not valid.</summary>
    private CleaningOptions? BuildOptions()
    {
        double? speed = null, spike = null, radius = null, window = null;
        if (RemoveJumps && HasTimes && !TryLimit(MaxSpeedText, 1, 1000, out speed))
        {
            Error = Strings.CleanInvalidSpeed;
            return null;
        }

        if (RemoveSpikes && !TryLimit(SpikeText, 1, 10_000, out spike))
        {
            Error = Strings.CleanInvalidDistance;
            return null;
        }

        if (RemoveStops && !TryLimit(StopRadiusText, 1, 1000, out radius))
        {
            Error = Strings.CleanInvalidDistance;
            return null;
        }

        if (SmoothElevation && HasElevations && !TryLimit(SmoothWindowText, 10, 5000, out window))
        {
            Error = Strings.CleanInvalidWindow;
            return null;
        }

        return new CleaningOptions(speed, spike, radius, window);
    }

    private static bool TryLimit(string text, double min, double max, out double? value)
    {
        value = null;
        if (PointInput.TryParseNumber(text, out var number) && number >= min && number <= max)
        {
            value = number;
            return true;
        }

        return false;
    }

    private async Task ComputeAsync(CleaningOptions options, CancellationTokenSource computation)
    {
        var token = computation.Token;
        try
        {
            await Task.Delay(TypingPause, token);
            var plan = await Task.Run(() => TrackCleaner.Plan(_lists, options), token);
            if (ReferenceEquals(_computation, computation))
            {
                Plan = plan;
                IsComputing = false;
                _computation = null;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Superseded by newer options or the dialog closed.
        }
        finally
        {
            computation.Dispose();
        }
    }
}
