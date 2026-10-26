namespace Mendikat.TrailLab.Domain.Documents;

/// <summary>Reason a file could not be loaded. The UI maps each value to a user message.</summary>
public enum DocumentLoadError
{
    FileNotFound,
    AccessDenied,
    EmptyFile,
    ReadFailed,

    /// <summary>Not well-formed XML. <see cref="DocumentLoadException.Line"/> is set when known.</summary>
    InvalidXml,

    /// <summary>Well-formed XML whose root is not a GPX element.</summary>
    NotGpx,

    /// <summary>Missing, non-numeric or out-of-range latitude/longitude.</summary>
    InvalidCoordinate,

    /// <summary>A numeric field (e.g. elevation) that cannot be parsed.</summary>
    InvalidNumber,

    /// <summary>A date/time field that cannot be parsed.</summary>
    InvalidDate,
}

/// <summary>Expected failure while loading a document; carries a typed reason instead of a UI text.</summary>
public sealed class DocumentLoadException : Exception
{
    public DocumentLoadException(DocumentLoadError error, string? detail = null, int? line = null, Exception? innerException = null)
        : base(BuildMessage(error, detail, line), innerException)
    {
        Error = error;
        Detail = detail;
        Line = line;
    }

    public DocumentLoadError Error { get; }

    /// <summary>Optional technical detail (offending value, datum name). Must not contain whole file content.</summary>
    public string? Detail { get; }

    /// <summary>1-based line number in the file, when known.</summary>
    public int? Line { get; }

    private static string BuildMessage(DocumentLoadError error, string? detail, int? line) =>
        $"Document load failed: {error}" +
        (line is null ? string.Empty : $" at line {line}") +
        (detail is null ? string.Empty : $" ({detail})");
}
