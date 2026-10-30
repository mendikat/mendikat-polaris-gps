using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Polaris.Gps.App.Services;

/// <summary>The latest published version: its number, tag ("v0.5.0") and the page to download it from.</summary>
public sealed record ReleaseInfo(Version Version, string Tag, string PageUrl);

/// <summary>Asks which is the latest published version of Polaris GPS.</summary>
public interface IUpdateChecker
{
    /// <exception cref="UpdateCheckException">The service could not be reached or its answer is not understood.</exception>
    Task<ReleaseInfo> LatestAsync(CancellationToken cancellationToken);
}

public sealed class UpdateCheckException(string message, Exception? innerException = null) : Exception(message, innerException);

/// <summary>
/// The latest release of the public repository where the setup programs are published (the source code repository is
/// private): GitHub REST API <c>GET /repos/{repository}/releases/latest</c>, without credentials (60 requests per hour
/// and address). Drafts and pre-releases are not "latest". The tag is the version with a leading "v".
/// </summary>
public sealed class GitHubUpdateChecker(HttpClient http, string repository = GitHubUpdateChecker.Repository) : IUpdateChecker
{
    public const string Repository = "mendikat/mendikat-polaris-gps-application";

    /// <summary>Page with all the published versions, opened when the check cannot be made.</summary>
    public const string ReleasesPage = "https://github.com/" + Repository + "/releases";

    public async Task<ReleaseInfo> LatestAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repository}/releases/latest");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        try
        {
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new UpdateCheckException($"HTTP {(int)response.StatusCode}");
            }

            return Parse(body);
        }
        catch (HttpRequestException ex)
        {
            throw new UpdateCheckException(ex.Message, ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UpdateCheckException("timeout", ex);
        }
    }

    /// <summary>The release in a GitHub API answer (<c>tag_name</c>, <c>html_url</c>).</summary>
    public static ReleaseInfo Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var tag = document.RootElement.GetProperty("tag_name").GetString() ?? string.Empty;
            var page = document.RootElement.GetProperty("html_url").GetString() ?? ReleasesPage;
            return TryParseVersion(tag) is { } version
                ? new ReleaseInfo(version, tag, page)
                : throw new UpdateCheckException($"Unexpected tag '{tag}'");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new UpdateCheckException("Unexpected answer", ex);
        }
    }

    /// <summary>"v0.5.0" or "0.5.0" → 0.5.0 (missing parts count as 0); null if it is not a version.</summary>
    public static Version? TryParseVersion(string tag)
    {
        var text = tag.Trim().TrimStart('v', 'V');
        var dash = text.IndexOfAny(['-', '+']);
        if (dash >= 0)
        {
            text = text[..dash];
        }

        return Version.TryParse(text.Contains('.') ? text : text + ".0", out var version)
            ? new Version(version.Major, version.Minor, Math.Max(0, version.Build))
            : null;
    }
}
