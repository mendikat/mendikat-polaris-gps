using System.Globalization;
using Polaris.Gps.App.Resources;
using Polaris.Gps.App.Services;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Domain.Processing;

namespace Polaris.Gps.App.ViewModels;

/// <summary>
/// "Simplify" dialog: tolerance in meters and a live preview of the point count before applying (SIMP-04).
/// Nothing changes until the user accepts; the result can be undone.
/// Large tracks are computed in the background once typing pauses, so the dialog never freezes (NFR-02).
/// </summary>
public sealed class SimplifyViewModel : ObservableObject
{
    public const double DefaultTolerance = 5;

    /// <summary>Above this many points the preview is computed in the background.</summary>
    public const int DefaultBackgroundThreshold = 20_000;

    /// <summary>Pause after the last keystroke before a background computation starts.</summary>
    private static readonly TimeSpan TypingPause = TimeSpan.FromMilliseconds(300);

    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private readonly Lazy<PreparedSimplification> _prepared;
    private readonly bool _inBackground;
    private string _toleranceText = DefaultTolerance.ToString(Spanish);
    private SimplificationPlan? _plan;
    private string? _error;
    private bool _isComputing;
    private CancellationTokenSource? _computation;

    public SimplifyViewModel(LayerItemViewModel layer, int backgroundThreshold = DefaultBackgroundThreshold)
    {
        Layer = layer;
        IReadOnlyList<List<TrailPoint>> segments = layer.PointLists.Select(l => l.Points).ToList();
        _prepared = new Lazy<PreparedSimplification>(() => TrackSimplifier.Prepare(segments), LazyThreadSafetyMode.ExecutionAndPublication);
        _inBackground = segments.Sum(s => s.Count) > backgroundThreshold;
        Recompute();
    }

    public LayerItemViewModel Layer { get; }

    public string Title => string.Format(Strings.SimplifyTitle, Layer.Name);

    public string ToleranceText
    {
        get => _toleranceText;
        set
        {
            if (SetProperty(ref _toleranceText, value ?? string.Empty))
            {
                Recompute();
            }
        }
    }

    public SimplificationPlan? Plan
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

    /// <summary>True while a background computation for the current tolerance is pending.</summary>
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

    /// <summary>"532 → 118 puntos (se eliminan 414) con tolerancia 5 m".</summary>
    public string Preview => IsComputing
        ? Strings.SimplifyComputing
        : Plan is { } plan
            ? string.Format(Spanish, Strings.SimplifyPreview, plan.OriginalCount.ToString("N0", Spanish), plan.ResultCount.ToString("N0", Spanish),
                plan.RemovedCount.ToString("N0", Spanish), plan.ToleranceMeters.ToString("0.###", Spanish))
            : string.Empty;

    public bool CanApply => !IsComputing && Plan?.Command is not null;

    /// <summary>Stops a pending computation (also called when the dialog closes).</summary>
    public void Cancel()
    {
        _computation?.Cancel();
        _computation = null;
        IsComputing = false;
    }

    private void Recompute()
    {
        Cancel();
        if (!PointInput.TryParseNumber(ToleranceText, out var tolerance) || tolerance <= 0 || tolerance > 10_000)
        {
            Error = Strings.SimplifyInvalidTolerance;
            Plan = null;
            return;
        }

        Error = null;
        if (!_inBackground)
        {
            Plan = _prepared.Value.Plan(tolerance);
            return;
        }

        Plan = null;
        IsComputing = true;
        _computation = new CancellationTokenSource();
        Computation = ComputeAsync(tolerance, _computation);
    }

    private async Task ComputeAsync(double tolerance, CancellationTokenSource computation)
    {
        var token = computation.Token;
        try
        {
            await Task.Delay(TypingPause, token);
            var plan = await Task.Run(() => _prepared.Value.Plan(tolerance, token), token);
            if (ReferenceEquals(_computation, computation))
            {
                Plan = plan;
                IsComputing = false;
                _computation = null;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Superseded by a newer tolerance or the dialog closed: that computation shows the result.
        }
        finally
        {
            computation.Dispose();
        }
    }
}
