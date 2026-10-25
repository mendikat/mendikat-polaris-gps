namespace Mendikat.TrailLab.Gpx;

/// <summary>The generated file did not pass verification; nothing was written to the destination.</summary>
public sealed class SaveVerificationException(string message) : IOException(message);

/// <summary>
/// Writes a file without ever leaving the destination truncated (SAVE-03, ADR-0009):
/// temporary file in the same folder → flush to disk → atomic replace (File.Replace) or move.
/// If anything fails, the temporary file is deleted and the original file stays as it was.
/// </summary>
public static class SafeFileWriter
{
    public static void Write(string path, byte[] content)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
        var name = Path.GetFileName(fullPath);
        var id = Guid.NewGuid().ToString("N")[..8];
        var temporary = Path.Combine(directory, $"{name}.{id}.tmp");
        var backup = Path.Combine(directory, $"{name}.{id}.bak");

        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.WriteThrough))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(fullPath))
            {
                File.Replace(temporary, fullPath, backup, ignoreMetadataErrors: true);
                File.Delete(backup);
            }
            else
            {
                File.Move(temporary, fullPath);
            }
        }
        finally
        {
            DeleteQuietly(temporary);
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best effort: a leftover temporary file never affects the destination file.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above.
        }
    }
}
