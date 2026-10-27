using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Domain.Model;

namespace Mendikat.TrailLab.Gpx;

/// <summary>Writes GPX files, verified before they replace anything (SAVE-03). Used by Save and by Convert to GPX (CONV-06).</summary>
public static class GpxConversion
{
    /// <summary>Serializes, reads the result back to check it, and writes it safely.</summary>
    /// <exception cref="SaveVerificationException">The generated file did not match the data; nothing was written.</exception>
    /// <exception cref="IOException">The file could not be written; the destination is unchanged.</exception>
    /// <exception cref="UnauthorizedAccessException">No permission to write the destination.</exception>
    public static void Write(TrailData data, string filePath)
    {
        var bytes = TrailFileWriter.Serialize(data, DocumentFormat.Gpx);
        Write(bytes, TrailFileWriter.Shape(data), filePath);
    }

    /// <summary>Second half of <see cref="Write(TrailData, string)"/>, for callers that serialize on another thread.</summary>
    public static void Write(byte[] bytes, TrailShape shape, string filePath)
    {
        if (!TrailFileWriter.Verify(bytes, DocumentFormat.Gpx, shape))
        {
            throw new SaveVerificationException("The generated file does not match the document.");
        }

        SafeFileWriter.Write(filePath, bytes);
    }

    /// <summary>
    /// A free path for <paramref name="desired"/>: itself if it does not exist, else "name (2).gpx", "name (3).gpx"…
    /// </summary>
    public static string FreePath(string desired, Func<string, bool>? exists = null)
    {
        exists ??= File.Exists;
        if (!exists(desired))
        {
            return desired;
        }

        var directory = Path.GetDirectoryName(desired) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(desired);
        var extension = Path.GetExtension(desired);
        for (var n = 2; ; n++)
        {
            var candidate = Path.Combine(directory, $"{name} ({n}){extension}");
            if (!exists(candidate))
            {
                return candidate;
            }
        }
    }
}
