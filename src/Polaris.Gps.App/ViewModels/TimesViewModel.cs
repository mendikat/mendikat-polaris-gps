using System.Globalization;
using Polaris.Gps.App.Resources;
using Polaris.Gps.App.Services;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Domain.Processing;

namespace Polaris.Gps.App.ViewModels;

/// <summary>
/// "Regenerate times" dialog (TIME-01..06): start date, time and time zone, speed or uniform interval, and a preview
/// of the resulting times in local time and UTC. Nothing changes until the user accepts; the result can be undone.
/// </summary>
public sealed class TimesViewModel : ObservableObject
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private static readonly string[] TimeFormats = ["H:mm:ss", "H:mm"];
    private readonly IReadOnlyList<IReadOnlyList<TrailPoint>> _segments;
    private DateTime? _startDate;
    private string _startTimeText;
    private TimeZoneInfo _timeZone = TimeZoneInfo.Local;
    private TimeMode _mode = TimeMode.ByDistance;
    private string _speedText = "4";
    private SpeedUnitOption _speedUnit;
    private string _intervalText = "5";
    private bool _includeSegmentGaps = true;
    private TimePlan? _plan;
    private string? _error;

    public TimesViewModel(LayerItemViewModel layer)
    {
        Layer = layer;
        _segments = layer.PointLists.Select(l => (IReadOnlyList<TrailPoint>)l.Points).ToList();
        SpeedUnits =
        [
            new(Domain.Processing.SpeedUnit.KilometersPerHour, Strings.UnitKmh),
            new(Domain.Processing.SpeedUnit.MetersPerSecond, Strings.UnitMs),
            new(Domain.Processing.SpeedUnit.MinutesPerKilometer, Strings.UnitMinKm),
        ];
        _speedUnit = SpeedUnits[0];

        // Default start: the current first time of the track (in local time), or today at 09:00.
        var firstTime = _segments.SelectMany(s => s).FirstOrDefault(p => p.Time is not null)?.Time;
        var start = firstTime is { } t ? TimeZoneInfo.ConvertTime(t, _timeZone).DateTime : DateTime.Today.AddHours(9);
        _startDate = start.Date;
        _startTimeText = start.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        Recompute();
    }

    public LayerItemViewModel Layer { get; }

    public string Title => string.Format(Strings.TimesTitle, Layer.Name);

    public IReadOnlyList<TimeZoneInfo> TimeZones { get; } = TimeZoneInfo.GetSystemTimeZones();

    public IReadOnlyList<SpeedUnitOption> SpeedUnits { get; }

    public bool HasSeveralSegments => Layer.Model is Track { Segments.Count: > 1 };

    public DateTime? StartDate { get => _startDate; set => Set(ref _startDate, value); }

    public string StartTimeText { get => _startTimeText; set => Set(ref _startTimeText, value ?? string.Empty); }

    public TimeZoneInfo TimeZone { get => _timeZone; set => Set(ref _timeZone, value ?? TimeZoneInfo.Local); }

    /// <summary>Times from distance and a constant speed (TIME-02).</summary>
    public bool ByDistance
    {
        get => _mode == TimeMode.ByDistance;
        set => SetMode(TimeMode.ByDistance, value);
    }

    /// <summary>Times from distance and slope, MIDE method (<see cref="MideTime"/>); the speed is the speed on the flat.</summary>
    public bool BySlope
    {
        get => _mode == TimeMode.BySlope;
        set => SetMode(TimeMode.BySlope, value);
    }

    /// <summary>Uniform interval between points (TIME-03).</summary>
    public bool Uniform
    {
        get => _mode == TimeMode.UniformInterval;
        set => SetMode(TimeMode.UniformInterval, value);
    }

    /// <summary>The speed applies to distance and slope modes.</summary>
    public bool UsesSpeed => _mode != TimeMode.UniformInterval;

    private void SetMode(TimeMode mode, bool chosen)
    {
        if (!chosen || !Set(ref _mode, mode, nameof(ByDistance)))
        {
            return;
        }

        OnPropertyChanged(nameof(BySlope));
        OnPropertyChanged(nameof(Uniform));
        OnPropertyChanged(nameof(UsesSpeed));
    }

    public string SpeedText { get => _speedText; set => Set(ref _speedText, value ?? string.Empty); }

    public SpeedUnitOption SelectedSpeedUnit { get => _speedUnit; set => Set(ref _speedUnit, value ?? SpeedUnits[0]); }

    /// <summary>Seconds between consecutive points in uniform mode.</summary>
    public string IntervalText { get => _intervalText; set => Set(ref _intervalText, value ?? string.Empty); }

    public bool IncludeSegmentGaps { get => _includeSegmentGaps; set => Set(ref _includeSegmentGaps, value); }

    public TimePlan? Plan
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

    public bool CanApply => Plan is not null;

    /// <summary>Start, end and duration in local time and UTC, how many times are replaced, and time zone notes.</summary>
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
                string.Format(Spanish, Strings.TimesPreviewStart, Local(plan.StartUtc), Utc(plan.StartUtc)),
                string.Format(Spanish, Strings.TimesPreviewEnd, Local(plan.EndUtc), Utc(plan.EndUtc)),
                string.Format(Spanish, Strings.TimesPreviewDuration, Duration(plan.Duration), plan.PointCount.ToString("N0", Spanish)),
            };
            if (plan.PointsWithPreviousTime > 0)
            {
                lines.Add(string.Format(Spanish, Strings.TimesPreviewReplaced, plan.PointsWithPreviousTime.ToString("N0", Spanish)));
            }

            if (plan.StartIssue == TimeStartIssue.Ambiguous)
            {
                lines.Add(Strings.TimesAmbiguousStart);
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    private string Local(DateTimeOffset utc) =>
        TimeZoneInfo.ConvertTime(utc, TimeZone).ToString("dd/MM/yyyy HH:mm:ss", Spanish);

    private static string Utc(DateTimeOffset utc) => utc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC";

    private static string Duration(TimeSpan duration) =>
        duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours} h {duration.Minutes:00} min {duration.Seconds:00} s"
            : $"{duration.Minutes} min {duration.Seconds:00} s";

    private bool Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (!SetProperty(ref field, value, name))
        {
            return false;
        }

        Recompute();
        return true;
    }

    private void Recompute()
    {
        Error = Validate(out var options);
        if (Error is not null)
        {
            Plan = null;
            return;
        }

        Plan = TimeRegenerator.Plan(_segments, options!);
        if (Plan is null && _segments.All(s => s.Count == 0))
        {
            Error = Strings.ProfileNoPoints;
        }
    }

    private string? Validate(out TimeOptions? options)
    {
        options = null;
        if (StartDate is not { } date
            || !DateTime.TryParseExact(StartTimeText.Trim(), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return Strings.TimesInvalidStart;
        }

        var start = date.Date + time.TimeOfDay;
        if (TimeRegenerator.CheckStart(start, TimeZone) == TimeStartIssue.Invalid)
        {
            return Strings.TimesInvalidDaylightStart;
        }

        double speed = 0;
        var interval = TimeSpan.Zero;
        if (UsesSpeed)
        {
            if (!PointInput.TryParseNumber(SpeedText, out speed) || speed <= 0 || speed > 1000)
            {
                return Strings.TimesInvalidSpeed;
            }
        }
        else
        {
            if (!PointInput.TryParseNumber(IntervalText, out var seconds) || seconds <= 0 || seconds > 86_400)
            {
                return Strings.TimesInvalidInterval;
            }

            interval = TimeSpan.FromSeconds(seconds);
        }

        options = new TimeOptions(start, TimeZone, _mode,
            speed, SelectedSpeedUnit.Unit, interval, IncludeSegmentGaps);
        return null;
    }
}

/// <summary>A speed unit with its display name.</summary>
public sealed record SpeedUnitOption(SpeedUnit Unit, string DisplayName)
{
    public override string ToString() => DisplayName;
}
