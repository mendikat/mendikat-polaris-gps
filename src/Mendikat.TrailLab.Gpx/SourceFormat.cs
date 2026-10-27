namespace Mendikat.TrailLab.Gpx;

/// <summary>Format of a file that can be opened (CONV-01). Only GPX is written; the others are converted to GPX.</summary>
public enum SourceFormat
{
    Gpx,
    Kml,
    Kmz,
    Tcx,
}
