using System.IO;
using System.Text.RegularExpressions;
using Mendikat.TrailLab.App.ViewModels;
using Mendikat.TrailLab.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mendikat.TrailLab.Tests.App;

/// <summary>Help menu (APP-09): user guide, PayPal donation and "About".</summary>
public sealed partial class HelpMenuTests
{
    private readonly FakeDialogService _dialogs = new();
    private readonly MainViewModel _vm;

    public HelpMenuTests()
    {
        _vm = new MainViewModel(_dialogs, new FakeDocumentStore(), new FakeSettingsStore(), NullLogger<MainViewModel>.Instance);
    }

    [Fact]
    public void Help_opens_the_local_user_guide()
    {
        _vm.HelpCommand.Execute(null);

        var opened = Assert.Single(_dialogs.OpenedExternally);
        Assert.EndsWith(Path.Combine("Help", "ayuda.html"), opened);
        Assert.True(File.Exists(opened));
        Assert.Empty(_dialogs.Errors);
    }

    [Fact]
    public void Donate_opens_the_paypal_button_of_mendikat()
    {
        _vm.DonateCommand.Execute(null);

        Assert.Equal("https://www.paypal.com/donate/?hosted_button_id=4BNLU9ACSHTFN", Assert.Single(_dialogs.OpenedExternally));
    }

    [Fact]
    public void Web_opens_mendikat_net()
    {
        _vm.WebCommand.Execute(null);

        Assert.Equal("https://www.mendikat.net", Assert.Single(_dialogs.OpenedExternally));
    }

    [Fact]
    public void Check_for_updates_opens_mendikat_net_for_now()
    {
        _vm.CheckUpdatesCommand.Execute(null);

        Assert.Equal("https://www.mendikat.net", Assert.Single(_dialogs.OpenedExternally));
    }

    [Fact]
    public void About_shows_version_author_and_links()
    {
        _vm.AboutCommand.Execute(null);

        var about = _dialogs.AboutShown!;
        Assert.Equal("Versión 0.0.5", about.VersionText);
        Assert.Equal("Autor: Javier Urrutia", about.AuthorText);
        Assert.Equal("mailto:javi@mendikat.net", about.AuthorEmailUri);
        Assert.Equal("https://www.mendikat.net", about.Web);
    }

    [Fact]
    public void The_user_guide_has_the_donation_form_and_working_internal_links()
    {
        var html = File.ReadAllText(MainViewModel.HelpFilePath);

        Assert.Contains("<form action=\"https://www.paypal.com/donate\" method=\"post\"", html);
        Assert.Contains("name=\"hosted_button_id\" value=\"4BNLU9ACSHTFN\"", html);
        Assert.Contains("mailto:javi@mendikat.net", html);
        Assert.Contains("href=\"https://www.mendikat.net\"", html);
        var ids = SectionIds().Matches(html).Select(m => m.Groups[1].Value).ToHashSet();
        var links = InternalLinks().Matches(html).Select(m => m.Groups[1].Value).ToList();
        Assert.NotEmpty(links);
        Assert.All(links, link => Assert.Contains(link, ids));
    }

    [GeneratedRegex("<section id=\"([a-z]+)\"")]
    private static partial Regex SectionIds();

    [GeneratedRegex("href=\"#([a-z]+)\"")]
    private static partial Regex InternalLinks();
}
