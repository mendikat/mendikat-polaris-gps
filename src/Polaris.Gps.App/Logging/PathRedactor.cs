namespace Polaris.Gps.App.Logging;

/// <summary>
/// Removes the user's profile folder from log text (NFR-06). System exception messages often include
/// full paths such as C:\Users\name\..., which would reveal personal information.
/// </summary>
public sealed class PathRedactor
{
    private const string Placeholder = "%USERPROFILE%";
    private readonly string _profilePath;

    public PathRedactor(string profilePath)
    {
        _profilePath = profilePath.TrimEnd('\\', '/');
    }

    public static PathRedactor ForCurrentUser() =>
        new(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    public string Redact(string text) =>
        _profilePath.Length == 0 ? text : text.Replace(_profilePath, Placeholder, StringComparison.OrdinalIgnoreCase);
}
