using Polaris.Gps.App.Services;
using Polaris.Gps.App.ViewModels;
using Polaris.Gps.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Polaris.Gps.Tests.App;

/// <summary>Help › Check for updates against the latest release of the public releases repository.</summary>
public sealed class UpdateCheckTests
{
    private readonly FakeDialogService _dialogs = new();

    private MainViewModel Create(Func<ReleaseInfo> latest) =>
        new(_dialogs, new FakeDocumentStore(), new FakeSettingsStore(), NullLogger<MainViewModel>.Instance) { UpdateChecker = new Checker(latest) };

    [Fact]
    public async Task A_newer_version_offers_its_page()
    {
        var vm = Create(() => new ReleaseInfo(new Version(99, 0, 0), "v99.0.0", "https://github.com/mendikat/polaris-gps-releases/releases/tag/v99.0.0"));

        await vm.CheckUpdatesAsync();

        Assert.StartsWith("Hay una versión nueva de Polaris GPS: 99.0.0", Assert.Single(_dialogs.Confirmations));
        Assert.Equal("https://github.com/mendikat/polaris-gps-releases/releases/tag/v99.0.0", Assert.Single(_dialogs.OpenedExternally));
    }

    [Fact]
    public async Task The_same_version_says_it_is_the_latest()
    {
        var current = GitHubUpdateChecker.TryParseVersion(AboutViewModel.Version)!;
        var vm = Create(() => new ReleaseInfo(current, "v" + current, "x"));

        await vm.CheckUpdatesAsync();

        Assert.StartsWith("Tienes la última versión", Assert.Single(_dialogs.Infos));
        Assert.Empty(_dialogs.OpenedExternally);
    }

    [Fact]
    public async Task When_the_check_fails_the_releases_page_is_offered()
    {
        var vm = Create(() => throw new UpdateCheckException("offline"));

        await vm.CheckUpdatesAsync();

        Assert.StartsWith("No se pudo comprobar", Assert.Single(_dialogs.Confirmations));
        Assert.Equal(GitHubUpdateChecker.ReleasesPage, Assert.Single(_dialogs.OpenedExternally));
    }

    [Fact]
    public void Tags_and_github_answers_are_read()
    {
        Assert.Equal(new Version(0, 5, 0), GitHubUpdateChecker.TryParseVersion("v0.5"));
        Assert.Equal(new Version(1, 2, 3), GitHubUpdateChecker.TryParseVersion("1.2.3+abc"));
        Assert.Null(GitHubUpdateChecker.TryParseVersion("latest"));

        var release = GitHubUpdateChecker.Parse("""{"tag_name":"v0.5.0","html_url":"https://github.com/mendikat/polaris-gps-releases/releases/tag/v0.5.0","draft":false}""");
        Assert.Equal(new Version(0, 5, 0), release.Version);
        Assert.Throws<UpdateCheckException>(() => GitHubUpdateChecker.Parse("""{"message":"Not Found"}"""));
    }

    private sealed class Checker(Func<ReleaseInfo> latest) : IUpdateChecker
    {
        public Task<ReleaseInfo> LatestAsync(CancellationToken cancellationToken)
        {
            try
            {
                return Task.FromResult(latest());
            }
            catch (UpdateCheckException ex)
            {
                return Task.FromException<ReleaseInfo>(ex);
            }
        }
    }
}
