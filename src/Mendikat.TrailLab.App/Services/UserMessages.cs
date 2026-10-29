using Mendikat.TrailLab.App.Resources;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Model;
using Mendikat.TrailLab.Gpx;
using Mendikat.TrailLab.Mapping.Local;

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
            DocumentLoadError.UnsupportedFormat => Strings.ErrorUnsupportedFormat,
            DocumentLoadError.InvalidArchive => Strings.ErrorInvalidArchive,
            DocumentLoadError.UnsupportedDatum => Strings.ErrorUnsupportedDatum,
            _ => Strings.ErrorReadFailed,
        };
        var location = exception.Line is { } line ? string.Format(Strings.AtLine, line) : string.Empty;
        return string.Format(template, fileName, location, exception.Detail ?? string.Empty);
    }

    /// <summary>Why a local map or DEM could not be opened (LMAP-02): names the file and the cause.</summary>
    public static string ForLocalMapError(LocalMapException exception, string fileName)
    {
        var template = exception.Error switch
        {
            LocalMapError.FileNotFound => Strings.ErrorFileNotFound,
            LocalMapError.EmptyFile => Strings.ErrorEmptyFile,
            LocalMapError.UnsupportedFormat => Strings.ErrorLocalUnsupportedFormat,
            LocalMapError.InvalidCalibration => Strings.ErrorLocalInvalidCalibration,
            LocalMapError.ImageMissing => Strings.ErrorLocalImageMissing,
            LocalMapError.UnsupportedImage => Strings.ErrorLocalUnsupportedImage,
            LocalMapError.InvalidImage => Strings.ErrorLocalInvalidImage,
            LocalMapError.UnsupportedCrs => Strings.ErrorLocalUnsupportedCrs,
            LocalMapError.MissingProjection => Strings.ErrorLocalMissingProjection,
            LocalMapError.InvalidDem => Strings.ErrorLocalInvalidDem,
            LocalMapError.TooLarge => Strings.ErrorLocalTooLarge,
            LocalMapError.GdalUnavailable => Strings.ErrorLocalGdalUnavailable,
            LocalMapError.EcwUnavailable => Strings.ErrorLocalEcwUnavailable,
            LocalMapError.EcwLicenseNotAccepted => Strings.ErrorLocalEcwLicense,
            LocalMapError.NotGeoreferenced => Strings.ErrorLocalNotGeoreferenced,
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
        LoadWarningKind.ConvertedFrom => string.Format(Strings.WarningConvertedFrom, warning.Detail),
        LoadWarningKind.IgnoredFeatures => string.Format(Strings.WarningIgnoredFeatures, warning.Count),
        LoadWarningKind.PointsWithoutPosition => string.Format(Strings.WarningPointsWithoutPosition, warning.Count),
        LoadWarningKind.DataNotConverted => string.Format(Strings.WarningDataNotConverted, warning.Count),
        _ => warning.Kind.ToString(),
    };

    public static string ForSaveIssue(SaveIssue issue) => issue.Kind switch
    {
        SaveIssueKind.GpxVersionUpgrade => Strings.SaveIssueGpxUpgrade,
        _ => issue.Kind.ToString(),
    };

    public static string FormatName(DocumentFormat format) => format switch
    {
        DocumentFormat.Gpx => "GPX",
        _ => format.ToString(),
    };

    /// <summary>Format as read, e.g. "GPX 1.1".</summary>
    public static string SourceFormat(DocumentFormat format, TrailData data) => data.FormatState switch
    {
        GpxFormatState { ConvertedFrom: { } source } => string.Format(Strings.FormatConverted, source),
        GpxFormatState gpx => string.Format(Strings.FormatGpx, gpx.SourceVersion),
        _ => FormatName(format),
    };
}
