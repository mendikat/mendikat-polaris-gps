using System.Text.RegularExpressions;
using System.Xml.Linq;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Domain.Model;

namespace Mendikat.TrailLab.Gpx;

/// <summary>
/// Reads KML 2.x with Google Earth's gx: extensions and converts it to GPX data (CONV-03). Follows mendikat.net's
/// model (each placemark line → a track, each point → a waypoint) without its defects (D11): every placemark of every
/// folder, lon,lat order, missing altitude → no elevation, all the lines of a MultiGeometry.
/// </summary>
public sealed partial class KmlReader
{
    public const string GxNamespace = "http://www.google.com/kml/ext/2.2";

    private static readonly XNamespace Gx = GxNamespace;

    private int _timesWithoutZone;
    private int _ignored;
    private XNamespace _ns = XNamespace.None;

    /// <exception cref="DocumentLoadException">Not a valid KML.</exception>
    /// <exception cref="OperationCanceledException">Cancelled.</exception>
    public TrailFileResult Read(Stream stream, IProgress<double>? progress = null, CancellationToken cancellationToken = default) =>
        Read(stream, SourceFormat.Kml, progress, cancellationToken);

    internal TrailFileResult Read(Stream stream, SourceFormat source, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var report = new LoadProgress(progress, cancellationToken);
        report.Report(0);
        var root = XmlInput.LoadRoot(stream.CanSeek ? new ProgressStream(stream, f => report.Report(f * 0.5)) : stream);
        if (root.Name.LocalName != "kml")
        {
            throw new DocumentLoadException(DocumentLoadError.UnsupportedFormat, root.Name.LocalName);
        }

        _ns = root.Name.Namespace;
        _timesWithoutZone = 0;

        // Overlays and network links live outside placemarks; polygons and models are counted where they are found.
        _ignored = root.Descendants().Count(e => e.Name.Namespace == _ns
            && e.Name.LocalName is "GroundOverlay" or "ScreenOverlay" or "PhotoOverlay" or "NetworkLink");

        var document = root.Element(_ns + "Document") ?? root.Element(_ns + "Folder");
        var data = ConvertedData.Create(source, XmlInput.Text(document?.Element(_ns + "name")), XmlInput.Text(document?.Element(_ns + "description")));

        var placemarks = root.Descendants(_ns + "Placemark").ToList();
        for (var i = 0; i < placemarks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadPlacemark(placemarks[i], data);
            report.Report(0.5 + (0.5 * (i + 1) / placemarks.Count));
        }

        LoadWarning[] specific = _ignored > 0 ? [new LoadWarning(LoadWarningKind.IgnoredFeatures, _ignored)] : [];
        report.Report(1);
        return new TrailFileResult(data, ConvertedData.Warnings(source, data, _timesWithoutZone, specific));
    }

    private void ReadPlacemark(XElement placemark, TrailData data)
    {
        var name = XmlInput.Text(placemark.Element(_ns + "name"));
        var description = XmlInput.Text(placemark.Element(_ns + "description"));
        var track = new Track { Name = name, Description = description };
        foreach (var geometry in placemark.Elements())
        {
            ReadGeometry(geometry, name, description, data, track);
        }

        if (track.Segments.Count > 0)
        {
            data.Tracks.Add(track);
        }
    }

    /// <summary>Points become waypoints; lines (LineString, gx:Track) become segments of the placemark's track.</summary>
    private void ReadGeometry(XElement geometry, string? name, string? description, TrailData data, Track track)
    {
        if (geometry.Name.Namespace == _ns)
        {
            switch (geometry.Name.LocalName)
            {
                case "Point":
                    if (Coordinates(geometry, firstOnly: true).FirstOrDefault() is { } point)
                    {
                        point.Name = name;
                        point.Description = description;
                        data.Waypoints.Add(point);
                    }

                    break;
                case "LineString":
                case "LinearRing":
                    AddSegment(track, Coordinates(geometry));
                    break;
                case "MultiGeometry":
                    foreach (var child in geometry.Elements())
                    {
                        ReadGeometry(child, name, description, data, track);
                    }

                    break;
                case "Polygon":
                case "Model":
                    _ignored++;
                    break;
            }
        }
        else if (geometry.Name.Namespace == Gx)
        {
            switch (geometry.Name.LocalName)
            {
                case "Track":
                    AddSegment(track, GxTrack(geometry));
                    break;
                case "MultiTrack":
                    foreach (var child in geometry.Elements(Gx + "Track"))
                    {
                        AddSegment(track, GxTrack(child));
                    }

                    break;
            }
        }
    }

    private static void AddSegment(Track track, List<TrailPoint> points)
    {
        if (points.Count == 0)
        {
            return;
        }

        var segment = new TrackSegment();
        segment.Points.AddRange(points);
        track.Segments.Add(segment);
    }

    /// <summary>The "lon,lat[,alt]" tuples of a geometry's coordinates element, separated by white space.</summary>
    /// <param name="firstOnly">A Point has one position: like Google Earth, anything after the first tuple is ignored.</param>
    private List<TrailPoint> Coordinates(XElement geometry, bool firstOnly = false)
    {
        var element = geometry.Element(_ns + "coordinates");
        if (element is null)
        {
            return [];
        }

        // Spaces around the commas ("-6.36669,  38.04557,1063") are common (mendikat.net writes them) and Google
        // Earth accepts them: join them first so that only the spaces between tuples separate points.
        var tuples = SpacesAroundCommas().Replace(element.Value, ",").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return tuples.Take(firstOnly ? 1 : tuples.Length)
            .Select(tuple => Point(tuple.Split(','), tuple, element))
            .ToList();
    }

    /// <summary>A gx:Track: positions in gx:coord ("lon lat alt") and, when there is one per position, times in when.</summary>
    private List<TrailPoint> GxTrack(XElement track)
    {
        var points = track.Elements(Gx + "coord")
            .Select(c => Point(c.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries), c.Value, c))
            .ToList();
        var times = track.Elements(_ns + "when").ToList();
        if (times.Count == points.Count)
        {
            for (var i = 0; i < points.Count; i++)
            {
                points[i].Time = XmlInput.OptionalTime(times[i], ref _timesWithoutZone);
            }
        }

        return points;
    }

    private static TrailPoint Point(string[] parts, string text, XElement element)
    {
        if (parts.Length < 2
            || !XmlInput.TryParseDouble(parts[0], out var lon) || !GeoPosition.IsValidLongitude(lon)
            || !XmlInput.TryParseDouble(parts[1], out var lat) || !GeoPosition.IsValidLatitude(lat))
        {
            throw new DocumentLoadException(DocumentLoadError.InvalidCoordinate, XmlInput.Shorten(text.Trim()), XmlInput.LineOf(element));
        }

        var point = new TrailPoint(new GeoPosition(lat, lon));
        if (parts.Length > 2 && parts[2].Trim().Length > 0)
        {
            point.Elevation = XmlInput.TryParseDouble(parts[2], out var altitude)
                ? altitude
                : throw new DocumentLoadException(DocumentLoadError.InvalidNumber, XmlInput.Shorten(text.Trim()), XmlInput.LineOf(element));
        }

        return point;
    }

    [GeneratedRegex(@"\s*,\s*")]
    private static partial Regex SpacesAroundCommas();
}
