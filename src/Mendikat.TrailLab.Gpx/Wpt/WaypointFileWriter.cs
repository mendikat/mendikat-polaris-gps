using System.Text;
using Mendikat.TrailLab.Domain.Model;

namespace Mendikat.TrailLab.Gpx.Wpt;

/// <summary>Writes a WPT document in its original dialect and encoding, with CRLF line endings (WPT-04, WPT-05).</summary>
public static class WaypointFileWriter
{
    public static IReadOnlyList<SaveIssue> Check(TrailData data)
    {
        var state = StateOf(data);
        var issues = new List<SaveIssue>();
        var unrepresentable = CountUnrepresentable(string.Concat(Lines(data, state)), state.Encoding);
        if (unrepresentable > 0)
        {
            issues.Add(new SaveIssue(SaveIssueKind.UnrepresentableCharacters, unrepresentable));
        }

        var missingElevation = state.DialectId == "compegps" ? data.Waypoints.Count(w => w.Elevation is null) : 0;
        if (missingElevation > 0)
        {
            issues.Add(new SaveIssue(SaveIssueKind.MissingElevationWrittenAsZero, missingElevation));
        }

        return issues;
    }

    public static byte[] Write(TrailData data)
    {
        var state = StateOf(data);
        var text = string.Concat(Lines(data, state).Select(l => l + "\r\n"));
        // UTF-8 keeps its original BOM choice; ANSI code pages replace unrepresentable characters with "?".
        var encoding = state.Encoding is UTF8Encoding
            ? state.Encoding
            : Encoding.GetEncoding(state.Encoding.CodePage, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(text)];
    }

    private static IEnumerable<string> Lines(TrailData data, WptFormatState state)
    {
        foreach (var header in state.HeaderLines)
        {
            yield return header;
        }

        var number = 1;
        foreach (var point in data.Waypoints)
        {
            var lines = state.DialectId == "ozi"
                ? WaypointLineWriter.OziExplorer(point, number)
                : WaypointLineWriter.CompeGps(point);
            foreach (var line in lines)
            {
                yield return line;
            }

            number++;
        }
    }

    private static WptFormatState StateOf(TrailData data) =>
        data.FormatState as WptFormatState
        ?? throw new InvalidOperationException("Only documents read from a WPT file can be written as WPT.");

    private static int CountUnrepresentable(string text, Encoding encoding)
    {
        if (encoding is UTF8Encoding or UnicodeEncoding)
        {
            return 0;
        }

        var strict = Encoding.GetEncoding(encoding.CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        return text.EnumerateRunes().Where(rune => !rune.IsAscii).Count(rune =>
        {
            try
            {
                strict.GetBytes(rune.ToString());
                return false;
            }
            catch (EncoderFallbackException)
            {
                return true;
            }
        });
    }
}
