using System.Globalization;
using System.Xml.Linq;
using Polaris.Gps.Domain.Documents;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;

namespace Polaris.Gps.Gpx;

/// <summary>
/// Reads Garmin Training Center (TCX v2) files and converts them to GPX data (CONV-05). Each activity becomes a track
/// with one segment per lap; each course becomes a track and its course points waypoints. Heart rate and cadence go
/// to Garmin's TrackPointExtension v1, the usual place in GPX; trackpoints without position are skipped and counted.
/// </summary>
public sealed class TcxReader
{
    public const string TcxNamespace = "http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2";
    public const string TrackPointExtensionNamespace = "http://www.garmin.com/xmlschemas/TrackPointExtension/v1";
    public const string TrackPointExtensionPrefix = "gpxtpx";

    private static readonly XNamespace Gpx = GpxReader.Gpx11Namespace;
    private static readonly XNamespace Tpx = TrackPointExtensionNamespace;

    private XNamespace _ns = XNamespace.None;
    private int _timesWithoutZone;
    private int _withoutPosition;
    private int _notConverted;

    /// <exception cref="DocumentLoadException">Not a valid TCX.</exception>
    /// <exception cref="OperationCanceledException">Cancelled.</exception>
    public TrailFileResult Read(Stream stream, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var report = new LoadProgress(progress, cancellationToken);
        report.Report(0);
        var root = XmlInput.LoadRoot(stream.CanSeek ? new ProgressStream(stream, f => report.Report(f * 0.5)) : stream);
        if (root.Name.LocalName != "TrainingCenterDatabase")
        {
            throw new DocumentLoadException(DocumentLoadError.UnsupportedFormat, root.Name.LocalName);
        }

        _ns = root.Name.Namespace;
        _timesWithoutZone = _withoutPosition = _notConverted = 0;
        var data = ConvertedData.Create(SourceFormat.Tcx, null, null, (TrackPointExtensionPrefix, TrackPointExtensionNamespace));

        var activities = root.Elements(_ns + "Activities").Elements(_ns + "Activity").ToList();
        var courses = root.Elements(_ns + "Courses").Elements(_ns + "Course").ToList();
        var total = Math.Max(1, activities.Count + courses.Count);
        var done = 0;
        foreach (var activity in activities)
        {
            AddTrack(data, ReadActivity(activity));
            report.Report(0.5 + (0.5 * ++done / total));
        }

        foreach (var course in courses)
        {
            AddTrack(data, ReadCourse(course, data));
            report.Report(0.5 + (0.5 * ++done / total));
        }

        var specific = new List<LoadWarning>();
        if (_withoutPosition > 0)
        {
            specific.Add(new LoadWarning(LoadWarningKind.PointsWithoutPosition, _withoutPosition));
        }

        if (_notConverted > 0)
        {
            specific.Add(new LoadWarning(LoadWarningKind.DataNotConverted, _notConverted));
        }

        report.Report(1);
        return new TrailFileResult(data, ConvertedData.Warnings(SourceFormat.Tcx, data, _timesWithoutZone, specific));
    }

    private static void AddTrack(TrailData data, Track track)
    {
        if (track.Segments.Count > 0)
        {
            data.Tracks.Add(track);
        }
    }

    /// <summary>Name = Id (the start time), type = Sport, one segment per lap (its Track elements joined).</summary>
    private Track ReadActivity(XElement activity)
    {
        var track = new Track
        {
            Name = XmlInput.Text(activity.Element(_ns + "Id")),
            Type = (string?)activity.Attribute("Sport"),
            Description = XmlInput.Text(activity.Element(_ns + "Notes")),
        };
        foreach (var lap in activity.Elements(_ns + "Lap"))
        {
            AddSegment(track, lap.Elements(_ns + "Track").Elements(_ns + "Trackpoint"));
        }

        return track;
    }

    /// <summary>Name = Name, one segment per Track; course points become waypoints.</summary>
    private Track ReadCourse(XElement course, TrailData data)
    {
        var track = new Track
        {
            Name = XmlInput.Text(course.Element(_ns + "Name")),
            Description = XmlInput.Text(course.Element(_ns + "Notes")),
        };
        foreach (var segment in course.Elements(_ns + "Track"))
        {
            AddSegment(track, segment.Elements(_ns + "Trackpoint"));
        }

        foreach (var coursePoint in course.Elements(_ns + "CoursePoint"))
        {
            if (Position(coursePoint) is not { } position)
            {
                _withoutPosition++;
                continue;
            }

            data.Waypoints.Add(new TrailPoint(position)
            {
                Name = XmlInput.Text(coursePoint.Element(_ns + "Name")),
                Time = XmlInput.OptionalTime(coursePoint.Element(_ns + "Time"), ref _timesWithoutZone),
                Elevation = XmlInput.OptionalDouble(coursePoint.Element(_ns + "AltitudeMeters")),
                Type = XmlInput.Text(coursePoint.Element(_ns + "PointType")),
                Description = XmlInput.Text(coursePoint.Element(_ns + "Notes")),
            });
        }

        return track;
    }

    private void AddSegment(Track track, IEnumerable<XElement> trackpoints)
    {
        var segment = new TrackSegment();
        foreach (var trackpoint in trackpoints)
        {
            if (ReadTrackpoint(trackpoint) is { } point)
            {
                segment.Points.Add(point);
            }
        }

        if (segment.Points.Count > 0)
        {
            track.Segments.Add(segment);
        }
    }

    private TrailPoint? ReadTrackpoint(XElement trackpoint)
    {
        if (Position(trackpoint) is not { } position)
        {
            _withoutPosition++;
            return null;
        }

        var point = new TrailPoint(position)
        {
            Time = XmlInput.OptionalTime(trackpoint.Element(_ns + "Time"), ref _timesWithoutZone),
            Elevation = XmlInput.OptionalDouble(trackpoint.Element(_ns + "AltitudeMeters")),
        };

        // Speed, power and RunCadence live in Garmin's ActivityExtension (any namespace, element "TPX").
        var tpx = trackpoint.Element(_ns + "Extensions")?.Elements().FirstOrDefault(e => e.Name.LocalName == "TPX");
        var heartRate = XmlInput.OptionalDouble(trackpoint.Element(_ns + "HeartRateBpm")?.Element(_ns + "Value"));
        var cadence = XmlInput.OptionalDouble(trackpoint.Element(_ns + "Cadence"))
            ?? XmlInput.OptionalDouble(tpx?.Elements().FirstOrDefault(e => e.Name.LocalName == "RunCadence"));
        if (heartRate is not null || cadence is not null)
        {
            var extension = new XElement(
                Tpx + "TrackPointExtension",
                new XAttribute(XNamespace.Xmlns + TrackPointExtensionPrefix, TrackPointExtensionNamespace),
                heartRate is { } hr ? new XElement(Tpx + "hr", Integer(hr)) : null,
                cadence is { } cad ? new XElement(Tpx + "cad", Integer(cad)) : null);
            var extensions = new XElement(Gpx + "extensions", extension);
            point.OtherElements.Add(new XmlFragment("extensions", extensions.ToString(SaveOptions.DisableFormatting)));
        }

        if (trackpoint.Element(_ns + "DistanceMeters") is not null
            || tpx?.Elements().Any(e => e.Name.LocalName is "Speed" or "Watts") == true)
        {
            _notConverted++;
        }

        return point;
    }

    /// <summary>The Position element (LatitudeDegrees, LongitudeDegrees), or null when absent.</summary>
    /// <exception cref="DocumentLoadException">Present but invalid.</exception>
    private GeoPosition? Position(XElement element)
    {
        var position = element.Element(_ns + "Position");
        if (position is null)
        {
            return null;
        }

        var latitude = position.Element(_ns + "LatitudeDegrees");
        var longitude = position.Element(_ns + "LongitudeDegrees");
        if (!XmlInput.TryParseDouble(latitude?.Value, out var lat) || !GeoPosition.IsValidLatitude(lat))
        {
            throw new DocumentLoadException(DocumentLoadError.InvalidCoordinate, $"LatitudeDegrees=\"{XmlInput.Shorten(latitude?.Value ?? string.Empty)}\"", XmlInput.LineOf(position));
        }

        if (!XmlInput.TryParseDouble(longitude?.Value, out var lon) || !GeoPosition.IsValidLongitude(lon))
        {
            throw new DocumentLoadException(DocumentLoadError.InvalidCoordinate, $"LongitudeDegrees=\"{XmlInput.Shorten(longitude?.Value ?? string.Empty)}\"", XmlInput.LineOf(position));
        }

        return new GeoPosition(lat, lon);
    }

    private static string Integer(double value) => Math.Round(value).ToString(CultureInfo.InvariantCulture);
}
