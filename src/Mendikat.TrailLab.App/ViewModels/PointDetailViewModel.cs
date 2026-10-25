using System.Xml;
using System.Xml.Linq;
using Mendikat.TrailLab.App.Resources;
using Mendikat.TrailLab.App.Services;
using Mendikat.TrailLab.Domain.Editing;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Domain.Model;

namespace Mendikat.TrailLab.App.ViewModels;

/// <summary>
/// Editable details of the selected point (LAY-04, EDIT-04, WPE-02). Changes are applied explicitly with
/// "Aplicar" as one undoable step; fields whose text was not changed keep their exact original value, so
/// editing the name never rounds the coordinates.
/// </summary>
public sealed class PointDetailViewModel : ObservableObject
{
    private readonly PointValues _original;
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

    public PointDetailViewModel(TrailPoint point, PointFieldSupport support)
    {
        Point = point;
        Support = support;
        _original = PointValues.From(point);
        _name = point.Name ?? string.Empty;
        _comment = point.Comment ?? string.Empty;
        _description = point.Description ?? string.Empty;
        _latitude = _originalLatitude = PointInput.FormatCoordinate(point.Position.Latitude);
        _longitude = _originalLongitude = PointInput.FormatCoordinate(point.Position.Longitude);
        _elevation = _originalElevation = PointInput.FormatElevation(point.Elevation);
        _timeUtc = _originalTime = PointInput.FormatUtc(point.Time);
        LocalTime = point.Time is null ? "—" : PointInput.FormatLocal(point.Time);
        OtherFields = DescribeOtherFields(point.OtherElements);
    }

    public TrailPoint Point { get; }

    public PointFieldSupport Support { get; }

    public bool ShowComment => Support.Comment;

    public bool IsAdded => Point.IsAdded;

    public string LocalTime { get; }

    /// <summary>Format fields without a typed property (sat, hdop, extensions…), read-only; "—" if none.</summary>
    public string OtherFields { get; }

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
        || _timeUtc != _originalTime;

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
        var lat = _original.Position.Latitude;
        var lon = _original.Position.Longitude;
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

        if (!Support.NameAllowsSpaces && _name.Any(char.IsWhiteSpace))
        {
            return Strings.ValidationNoSpaces;
        }

        if (!Support.TextAllowsCommas && (_name.Contains(',') || _description.Contains(',')))
        {
            return Strings.ValidationNoCommas;
        }

        result = new PointValues(
            new GeoPosition(lat, lon),
            elevation,
            time,
            Keep(_name, _original.Name),
            Support.Comment ? Keep(_comment, _original.Comment) : _original.Comment,
            Keep(_description, _original.Description));
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
