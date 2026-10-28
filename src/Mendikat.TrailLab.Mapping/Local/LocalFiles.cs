namespace Mendikat.TrailLab.Mapping.Local;

/// <summary>Finding and checking the image a calibration file refers to (LMAP-03, LMAP-06).</summary>
public static class LocalFiles
{
    /// <summary>Image formats that Windows decodes without extra software (D7). OZF, ECW, MrSID… are not among them.</summary>
    public static IReadOnlyList<string> ImageExtensions { get; } = [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff"];

    /// <summary>
    /// A file next to <paramref name="path"/> with the same name and another extension ("mapa.prj" for "mapa.tif"), or
    /// with the whole name plus the extension ("mapa.tif.prj"); in any mix of upper and lower case and ignoring spaces
    /// around the name ("MDT05-0112-H30-LIDAR .PRJ"). The folder is listed and compared here instead of using a search
    /// pattern, because a folder can be case-sensitive (extracted by some tools or WSL).
    /// </summary>
    public static string? FindSidecar(string path, params string[] extensions)
    {
        var full = Path.GetFullPath(path);
        var folder = Path.GetDirectoryName(full) ?? string.Empty;
        string[] wanted = [Path.GetFileNameWithoutExtension(full).Trim(), Path.GetFileName(full).Trim()];
        return Directory.EnumerateFiles(folder)
            .Where(f => extensions.Any(e => Path.GetExtension(f).Trim().Equals(e, StringComparison.OrdinalIgnoreCase)))
            .Where(f => !string.Equals(f, full, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(f => wanted.Contains(Path.GetFileNameWithoutExtension(f).Trim(), StringComparer.OrdinalIgnoreCase));
    }

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
