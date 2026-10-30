using System.Xml;
using System.Xml.Linq;
using Polaris.Gps.App.Resources;
using Polaris.Gps.App.Services;
using Polaris.Gps.Domain.Editing;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Mapping.Coordinates;

namespace Polaris.Gps.App.ViewModels;

/// <summary>
/// Editable details of the selected point (LAY-04, EDIT-04, WPE-02). Changes are applied explicitly with
/// "Aplicar" as one undoable step; fields whose text was not changed keep their exact original value, so
/// editing the name never rounds the coordinates.
/// </summary>
public sealed class PointDetailViewModel : ObservableObject
{
    private readonly PointValues _original;
    private readonly CoordinateDatum _datum;
    private readonly WaypointSymbolOption _originalSymbol;
    private WaypointSymbolOption _symbol;
    private readonly string _originalLatitude;
    private readonly string _originalLongitude;
    private readonly string _originalElevation;
    private readonly string _originalTime;
    private string _name;
    private string _comment;
    private string _description;
    private string _latitude;
    private string _longitude;
    private string _elevation;
    private string _timeUtc;
    private string? _error;

    /// <param name="isWaypoint">The point is a waypoint: its symbol is shown and can be changed.</param>
    public PointDetailViewModel(TrailPoint point, CoordinateDatum datum = CoordinateDatum.Wgs84, bool isWaypoint = false)
    {
        Point = point;
        _datum = datum;
        IsWaypoint = isWaypoint;
        (Symbols, _symbol) = WaypointViewModel.SymbolOptions(point.Symbol);
        _originalSymbol = _symbol;
        _original = PointValues.From(point);
        _name = point.Name ?? string.Empty;
        _comment = point.Comment ?? string.Empty;
        _description = point.Description ?? string.Empty;
        (_originalLatitude, _originalLongitude) = PointInput.FormatPosition(point.Position, datum);
        (_latitude, _longitude) = (_originalLatitude, _originalLongitude);
        _elevation = _originalElevation = PointInput.FormatElevation(point.Elevation);
        _timeUtc = _originalTime = PointInput.FormatUtc(point.Time);
        LocalTime = point.Time is null ? "—" : PointInput.FormatLocal(point.Time);
        ElevationOrigin = point.Elevation is null ? "—" : point.ElevationSource switch
        {
            ElevationSource.Interpolated => Strings.ElevationSourceInterpolated,
            ElevationSource.Dem => Strings.ElevationSourceDem,
            ElevationSource.Edited => Strings.ElevationSourceEdited,
            ElevationSource.Smoothed => Strings.ElevationSourceSmoothed,
            _ => Strings.ElevationSourceOriginal,
        };
        OtherFields = DescribeOtherFields(point.OtherElements);
    }

    public TrailPoint Point { get; }

    public bool IsAdded => Point.IsAdded;

    public string LocalTime { get; }

    /// <summary>Where the elevation comes from in this session: file, interpolated, DEM or typed (DEM-06).</summary>
    public string ElevationOrigin { get; }

    /// <summary>Format fields without a typed property (sat, hdop, extensions…), read-only; "—" if none.</summary>
    public string OtherFields { get; }

    public bool IsWaypoint { get; }

    /// <summary>Waypoints: the symbols to choose from (see <see cref="WaypointViewModel.SymbolOptions"/>).</summary>
    public IReadOnlyList<WaypointSymbolOption> Symbols { get; }

    public WaypointSymbolOption Symbol
    {
        get => _symbol;
        set
        {
            if (value is not null && SetProperty(ref _symbol, value))
            {
                Error = null;
                OnPropertyChanged(nameof(HasChanges));
            }
        }
    }

    public string Name { get => _name; set => SetText(ref _name, value); }

    public string Comment { get => _comment; set => SetText(ref _comment, value); }

    public string Description { get => _description; set => SetText(ref _description, value); }

    public string Latitude { get => _latitude; set => SetText(ref _latitude, value); }

    public string Longitude { get => _longitude; set => SetText(ref _longitude, value); }

    public string Elevation { get => _elevation; set => SetText(ref _elevation, value); }

    public string TimeUtc { get => _timeUtc; set => SetText(ref _timeUtc, value); }

    public string? Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    public bool HasChanges =>
        _name != (_original.Name ?? string.Empty)
        || _comment != (_original.Comment ?? string.Empty)
        || _description != (_original.Description ?? string.Empty)
        || _latitude != _originalLatitude
        || _longitude != _originalLongitude
        || _elevation != _originalElevation
        || _timeUtc != _originalTime
        || !ReferenceEquals(_symbol, _originalSymbol);

    /// <summary>Validates the texts (WPE-03). On failure sets <see cref="Error"/> and returns false.</summary>
    public bool TryBuild(out PointValues values)
    {
        values = _original;
        var error = Validate(out var result);
        Error = error;
        if (error is not null)
        {
            return false;
        }

        values = result;
        return true;
    }

    private string? Validate(out PointValues result)
    {
        result = _original;
        // Typed coordinates are in the display datum (File › Settings › Coordinates); an untouched pair keeps the exact position.
        var (lat, lon) = CoordinateDatums.FromWgs84(_original.Position, _datum);
        if (_latitude != _originalLatitude && !PointInput.TryParseLatitude(_latitude, out lat))
        {
            return Strings.ValidationLatitude;
        }

        if (_longitude != _originalLongitude && !PointInput.TryParseLongitude(_longitude, out lon))
        {
            return Strings.ValidationLongitude;
        }

        var elevation = _original.Elevation;
        if (_elevation != _originalElevation && !PointInput.TryParseElevation(_elevation, out elevation))
        {
            return Strings.ValidationElevation;
        }

        var time = _original.Time;
        if (_timeUtc != _originalTime && !PointInput.TryParseUtc(_timeUtc, out time))
        {
            return Strings.ValidationTime;
        }

        var position = _latitude == _originalLatitude && _longitude == _originalLongitude
            ? _original.Position
            : CoordinateDatums.ToWgs84(lat, lon, _datum);
        result = new PointValues(
            position,
            elevation,
            time,
            Keep(_name, _original.Name),
            Keep(_comment, _original.Comment),
            Keep(_description, _original.Description)) { Symbol = ReferenceEquals(_symbol, _originalSymbol) ? _original.Symbol : _symbol.GpxName };
        return null;
    }

    /// <summary>Empty text means "no value", unless the field was already absent or empty.</summary>
    private static string? Keep(string text, string? original) =>
        text == (original ?? string.Empty) ? original : (text.Length == 0 ? null : text);

    private void SetText(ref string field, string value)
    {
        if (SetProperty(ref field, value ?? string.Empty))
        {
            Error = null;
            OnPropertyChanged(nameof(HasChanges));
        }
    }

    private static string DescribeOtherFields(IReadOnlyList<XmlFragment> elements)
    {
        if (elements.Count == 0)
        {
            return "—";
        }

        return string.Join(" · ", elements.Select(e =>
        {
            try
            {
                var element = XElement.Parse(e.Xml);
                return element.HasElements || element.Value.Length > 40 ? e.LocalName : $"{e.LocalName} = {element.Value}";
            }
            catch (XmlException)
            {
                return e.LocalName;
            }
        }));
    }
}
