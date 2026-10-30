namespace Polaris.Gps.Gpx;

/// <summary>Format of a file that can be opened (CONV-01). Only GPX is written; the others are converted to GPX.</summary>
public enum SourceFormat
{
    Gpx,
    Kml,
    Kmz,
    Tcx,

    /// <summary>OziExplorer track (.plt) or waypoint (.wpt) file.</summary>
    OziExplorer,

    /// <summary>CompeGPS/TwoNav track (.trk) or waypoint (.wpt) file.</summary>
    CompeGps,
}
