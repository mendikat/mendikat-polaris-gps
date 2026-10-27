namespace Mendikat.TrailLab.Mapping.Local;

/// <summary>Finding and checking the image a calibration file refers to (LMAP-03, LMAP-06).</summary>
public static class LocalFiles
{
    /// <summary>Image formats that Windows decodes without extra software (D7). OZF, ECW, MrSID… are not among them.</summary>
    public static IReadOnlyList<string> ImageExtensions { get; } = [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff"];

    /// <summary>
    /// The image path as written (absolute, or relative to the calibration file), or else a file with the same name
    /// in the calibration file's folder (maps are often moved together with their image).
    /// </summary>
    /// <exception cref="LocalMapException">The image does not exist, or is in an unsupported format.</exception>
    public static string ResolveImage(string calibrationPath, string written)
    {
        if (string.IsNullOrWhiteSpace(written))
        {
            throw new LocalMapException(LocalMapError.InvalidCalibration, "no indica la imagen");
        }

        var folder = Path.GetDirectoryName(Path.GetFullPath(calibrationPath)) ?? string.Empty;
        // Windows paths written on another computer: the name is after the last "\" or "/".
        var name = written[(written.LastIndexOfAny(['\\', '/']) + 1)..];

        // An OZF or ECW image is reported as such even when it is missing: finding it would not help (D7).
        if (!ImageExtensions.Contains(Path.GetExtension(name).ToLowerInvariant()))
        {
            throw new LocalMapException(LocalMapError.UnsupportedImage, name);
        }

        string?[] candidates =
        [
            Path.IsPathFullyQualified(written) ? written : null,
            Path.IsPathFullyQualified(written) ? null : Path.Combine(folder, written),
            Path.Combine(folder, name),
        ];
        var found = candidates.FirstOrDefault(c => c is not null && File.Exists(c));
        return found ?? throw new LocalMapException(LocalMapError.ImageMissing, written);
    }
}
