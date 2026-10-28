using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Mendikat.TrailLab.App.Resources;
using Mendikat.TrailLab.App.Settings;
using Mendikat.TrailLab.App.ViewModels;
using Mendikat.TrailLab.App.Views;
using Mendikat.TrailLab.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mendikat.TrailLab.Tests.App;

/// <summary>File › Settings: the language of the application and its translations.</summary>
public sealed partial class SettingsTests
{
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeSettingsStore _settings = new();
    private readonly MainViewModel _vm;

    public SettingsTests()
    {
        _vm = new MainViewModel(_dialogs, new FakeDocumentStore(), _settings, NullLogger<MainViewModel>.Instance);
    }

    public static TheoryData<string> Translations => new(AppLanguage.All.Skip(1).Select(l => l.Code));

    [Fact]
    public void Spanish_is_the_default_language_and_comes_first()
    {
        Assert.Equal("es", new UserSettings().Language);
        Assert.Equal(["es", "ca", "eu", "fr", "pt"], AppLanguage.All.Select(l => l.Code));
        Assert.Equal("es", AppLanguage.FromCode(null).Code);
        Assert.Equal("es", AppLanguage.FromCode("klingon").Code);
        Assert.Equal("eu", AppLanguage.FromCode("EU").Code);
    }

    [Fact]
    public void Choosing_a_language_saves_it_and_says_so_in_that_language()
    {
        _dialogs.ToolDialog = dialog =>
        {
            var settings = Assert.IsType<SettingsViewModel>(dialog);
            Assert.Equal("es", settings.Language.Code);
            settings.Language = AppLanguage.FromCode("eu");
            return true;
        };

        _vm.SettingsCommand.Execute(null);

        Assert.Equal("eu", _settings.Current.Language);
        Assert.Contains("euskarara", Assert.Single(_dialogs.Infos), StringComparison.Ordinal);
    }

    [Fact]
    public void Cancelling_or_keeping_the_language_saves_nothing()
    {
        _dialogs.ToolDialog = dialog =>
        {
            ((SettingsViewModel)dialog).Language = AppLanguage.FromCode("fr");
            return false;
        };
        _vm.SettingsCommand.Execute(null);

        _dialogs.ToolDialog = _ => true;
        _vm.SettingsCommand.Execute(null);

        Assert.Equal(0, _settings.SaveCount);
        Assert.Empty(_dialogs.Infos);
    }

    [Fact]
    public void A_failed_save_is_reported()
    {
        _settings.FailSaving = true;
        _dialogs.ToolDialog = dialog =>
        {
            ((SettingsViewModel)dialog).Language = AppLanguage.FromCode("ca");
            return true;
        };

        _vm.SettingsCommand.Execute(null);

        Assert.Equal(Strings.StatusMapSaveSettingsFailed, Assert.Single(_dialogs.Errors));
    }

    [Fact]
    public void Every_language_has_a_flag()
    {
        Assert.All(AppLanguage.All, l => Assert.NotNull(LanguageFlags.For(l.Code)));
        Assert.Null(LanguageFlags.For("xx"));
    }

    [Theory]
    [MemberData(nameof(Translations))]
    public void A_translation_has_only_known_texts_and_keeps_their_placeholders(string code)
    {
        var neutral = Texts(CultureInfo.InvariantCulture);
        var translated = Texts(CultureInfo.GetCultureInfo(code));

        Assert.NotEmpty(translated);
        Assert.All(translated, pair =>
        {
            Assert.True(neutral.ContainsKey(pair.Key), $"{code}: unknown text {pair.Key}");
            var original = neutral[pair.Key];
            Assert.True(
                Placeholders(original).SetEquals(Placeholders(pair.Value)),
                $"{code}: {pair.Key} changes its placeholders");
            // File dialog filters: same number of "description|pattern" parts, with the same patterns.
            Assert.Equal(Patterns(original), Patterns(pair.Value));
        });
    }

    [Theory]
    [MemberData(nameof(Translations))]
    public void A_translation_covers_the_main_texts(string code)
    {
        var translated = Texts(CultureInfo.GetCultureInfo(code));

        // The legal ECW texts stay in English, as the licence is written.
        string[] untranslated = ["AppTitle", "AboutEcwNotice", "EcwLicenseTerms"];
        var missing = Texts(CultureInfo.InvariantCulture).Keys
            .Where(k => !translated.ContainsKey(k) && !untranslated.Contains(k))
            .Where(k => Regex.IsMatch(Texts(CultureInfo.InvariantCulture)[k], @"\p{L}{3,}"))
            .Where(k => k is not ("FormatGpx" or "WaypointsLayerName" or "UnitKmh" or "UnitMs"))
            .ToList();
        Assert.Empty(missing);
    }

    private static Dictionary<string, string> Texts(CultureInfo culture)
    {
        var set = Strings.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)
            ?? throw new MissingManifestResourceException(culture.Name);
        return set.Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => (string)e.Value!);
    }

    private static HashSet<string> Placeholders(string text) => [.. PlaceholderRegex().Matches(text).Select(m => m.Value)];

    private static string[] Patterns(string text)
    {
        var parts = text.Split('|');
        return parts.Length < 2 ? [] : [.. parts.Where((_, i) => i % 2 == 1)];
    }

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderRegex();
}
