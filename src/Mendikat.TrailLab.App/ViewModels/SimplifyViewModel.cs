using System.Globalization;
using Mendikat.TrailLab.App.Resources;
using Mendikat.TrailLab.App.Services;
using Mendikat.TrailLab.Domain.Model;
using Mendikat.TrailLab.Domain.Processing;

namespace Mendikat.TrailLab.App.ViewModels;

/// <summary>
/// "Simplify" dialog: tolerance in meters and a live preview of the point count before applying (SIMP-04).
/// Nothing changes until the user accepts; the result can be undone.
/// </summary>
public sealed class SimplifyViewModel : ObservableObject
{
    public const double DefaultTolerance = 5;

    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private readonly IReadOnlyList<List<TrailPoint>> _segments;
    private string _toleranceText = DefaultTolerance.ToString(Spanish);
    private SimplificationPlan? _plan;
    private string? _error;

    public SimplifyViewModel(LayerItemViewModel layer)
    {
        Layer = layer;
        _segments = layer.PointLists.Select(l => l.Points).ToList();
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

    /// <summary>"532 → 118 puntos (se eliminan 414) con tolerancia 5 m".</summary>
    public string Preview => Plan is { } plan
        ? string.Format(Spanish, Strings.SimplifyPreview, plan.OriginalCount.ToString("N0", Spanish), plan.ResultCount.ToString("N0", Spanish),
            plan.RemovedCount.ToString("N0", Spanish), plan.ToleranceMeters.ToString("0.###", Spanish))
        : string.Empty;

    public bool CanApply => Plan?.Command is not null;

    private void Recompute()
    {
        if (!PointInput.TryParseNumber(ToleranceText, out var tolerance) || tolerance <= 0 || tolerance > 10_000)
        {
            Error = Strings.SimplifyInvalidTolerance;
            Plan = null;
            return;
        }

        Error = null;
        Plan = TrackSimplifier.Plan(_segments, tolerance);
    }
}
