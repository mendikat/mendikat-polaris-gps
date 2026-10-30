using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Polaris.Gps.App.Settings;
using Polaris.Gps.App.ViewModels;
using Polaris.Gps.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Polaris.Gps.Tests.App;

/// <summary>Help menu (APP-09): user guide, PayPal donation and "About".</summary>
public sealed partial class HelpMenuTests
{
    private readonly FakeDialogService _dialogs = new();
    private readonly MainViewModel _vm;

    public HelpMenuTests()
    {
        _vm = new MainViewModel(_dialogs, new FakeDocumentStore(), new FakeSettingsStore(), NullLogger<MainViewModel>.Instance);
    }

    public static TheoryData<string> Languages => new(AppLanguage.All.Select(l => l.Code));

    [Theory]
    [InlineData("es-ES", "ayuda.html")]
    [InlineData("ca-ES", "ayuda.ca.html")]
    [InlineData("eu-ES", "ayuda.eu.html")]
    [InlineData("fr-FR", "ayuda.fr.html")]
    [InlineData("pt-PT", "ayuda.pt.html")]
    [InlineData("de-DE", "ayuda.html")]
    public void Help_opens_the_local_user_guide_in_the_language_of_the_interface(string culture, string file)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            _vm.HelpCommand.Execute(null);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }

        var opened = Assert.Single(_dialogs.OpenedExternally);
        Assert.EndsWith(Path.Combine("Help", file), opened);
        Assert.True(File.Exists(opened));
        Assert.Empty(_dialogs.Errors);
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_guide_has_the_sections_of_the_Spanish_one_and_links_to_the_other_languages(string code)
    {
        var spanish = File.ReadAllText(MainViewModel.HelpFileFor(CultureInfo.GetCultureInfo("es")));
        var path = MainViewModel.HelpFileFor(CultureInfo.GetCultureInfo(code));
        var html = File.ReadAllText(path);

        Assert.Equal(code == "es" ? "ayuda.html" : $"ayuda.{code}.html", Path.GetFileName(path));
        Assert.Contains($"<html lang=\"{code}\">", html);
        Assert.Contains("<link rel=\"stylesheet\" href=\"ayuda.css\">", html);
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(path)!, "ayuda.css")));
        Assert.Equal(
            SectionIds().Matches(spanish).Select(m => m.Groups[1].Value),
            SectionIds().Matches(html).Select(m => m.Groups[1].Value));
        var others = OtherLanguages().Matches(html).Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(AppLanguage.All.Count - 1, others.Count);
        Assert.All(others, file => Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(path)!, file)), file));
        Assert.DoesNotContain(Path.GetFileName(path), others);
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
        Assert.Equal("Versión 0.1.2", about.VersionText);
        Assert.Equal("Autor: Javier Urrutia", about.AuthorText);
        Assert.Equal("mailto:javi@mendikat.net", about.AuthorEmailUri);
        Assert.Equal("https://www.mendikat.net", about.Web);
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void The_user_guide_has_the_donation_form_and_working_internal_links(string code)
    {
        var html = File.ReadAllText(MainViewModel.HelpFileFor(CultureInfo.GetCultureInfo(code)));

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

    [GeneratedRegex("<a href=\"(ayuda(?:\\.[a-z]{2})?\\.html)\" hreflang=")]
    private static partial Regex OtherLanguages();
}
