using Mendikat.TrailLab.App.Resources;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Model;
using Mendikat.TrailLab.Gpx;
using Mendikat.TrailLab.Gpx.Wpt;

namespace Mendikat.TrailLab.App.Services;

/// <summary>Maps typed domain errors and warnings to Spanish user messages (APP-05, GPX-02).</summary>
public static class UserMessages
{
    public static string ForLoadError(DocumentLoadException exception, string fileName)
    {
        var template = exception.Error switch
        {
            DocumentLoadError.FileNotFound => Strings.ErrorFileNotFound,
            DocumentLoadError.AccessDenied => Strings.ErrorAccessDenied,
            DocumentLoadError.EmptyFile => Strings.ErrorEmptyFile,
            DocumentLoadError.InvalidXml => Strings.ErrorInvalidXml,
            DocumentLoadError.NotGpx => Strings.ErrorNotGpx,
            DocumentLoadError.InvalidCoordinate => Strings.ErrorInvalidCoordinate,
            DocumentLoadError.InvalidNumber => Strings.ErrorInvalidNumber,
            DocumentLoadError.InvalidDate => Strings.ErrorInvalidDate,
            DocumentLoadError.UnsupportedWptDialect => Strings.ErrorUnsupportedWptDialect,
            DocumentLoadError.UnsupportedDatum => Strings.ErrorUnsupportedDatum,
            DocumentLoadError.UnsupportedCoordinateSystem => Strings.ErrorUnsupportedCoordinateSystem,
            _ => Strings.ErrorReadFailed,
        };
        var location = exception.Line is { } line ? string.Format(Strings.AtLine, line) : string.Empty;
        return string.Format(template, fileName, location, exception.Detail ?? string.Empty);
    }

    public static string ForWarning(LoadWarning warning) => warning.Kind switch
    {
        LoadWarningKind.TimesWithoutZone => string.Format(Strings.WarningTimesWithoutZone, warning.Count),
        LoadWarningKind.Gpx10 => Strings.WarningGpx10,
        LoadWarningKind.GpxWithoutNamespace => Strings.WarningGpxWithoutNamespace,
        LoadWarningKind.NoData => Strings.WarningNoData,
        _ => warning.Kind.ToString(),
    };

    public static string ForSaveIssue(SaveIssue issue) => issue.Kind switch
    {
        SaveIssueKind.GpxVersionUpgrade => Strings.SaveIssueGpxUpgrade,
        SaveIssueKind.UnrepresentableCharacters => string.Format(Strings.SaveIssueUnrepresentable, issue.Count),
        SaveIssueKind.MissingElevationWrittenAsZero => string.Format(Strings.SaveIssueMissingElevation, issue.Count),
        _ => issue.Kind.ToString(),
    };

    public static string FormatName(DocumentFormat format) => format switch
    {
        DocumentFormat.Gpx => "GPX",
        DocumentFormat.Wpt => "WPT",
        _ => format.ToString(),
    };

    /// <summary>Format and dialect as read, e.g. "GPX 1.1" or "WPT (OziExplorer)".</summary>
    public static string SourceFormat(DocumentFormat format, TrailData data) => data.FormatState switch
    {
        GpxFormatState gpx => string.Format(Strings.FormatGpx, gpx.SourceVersion),
        WptFormatState { DialectId: "ozi" } => Strings.FormatWptOzi,
        WptFormatState { DialectId: "compegps" } => Strings.FormatWptCompeGps,
        _ => FormatName(format),
    };
}
