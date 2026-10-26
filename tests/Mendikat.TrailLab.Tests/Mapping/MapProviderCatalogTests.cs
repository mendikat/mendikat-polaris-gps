using System.Text.RegularExpressions;
using Mendikat.TrailLab.Mapping.Providers;

namespace Mendikat.TrailLab.Tests.Mapping;

public sealed partial class MapProviderCatalogTests
{
    [Fact]
    [Trait("Req", "MAP-03")]
    public void Default_provider_is_ign_spain()
    {
        Assert.Equal("Mapa IGN (España)", MapProviderCatalog.Default.DisplayName);
        Assert.Same(MapProviderCatalog.Default, MapProviderCatalog.All[0]);
    }

    [Fact]
    [Trait("Req", "MAP-02")]
    public void Catalog_contains_the_eight_agreed_providers_with_unique_ids()
    {
        string[] expected =
        [
            "Mapa IGN (España)", "CyclOSM", "OpenTopoMap", "OpenStreetMap", "Mapa satélite (ESRI)",
            "Mapa Topográfico (ESRI)", "Géoportail IGN (France)", "MtbMap",
        ];

        Assert.Equal(expected, MapProviderCatalog.All.Select(p => p.DisplayName));
        Assert.Equal(MapProviderCatalog.All.Count, MapProviderCatalog.All.Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    [Trait("Req", "MAP-06")]
    public void Every_provider_has_attribution_https_and_tile_placeholders()
    {
        Assert.All(MapProviderCatalog.All, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.AttributionText));
            Assert.StartsWith("https://", p.AttributionUrl);
            Assert.StartsWith("https://", p.UrlTemplate);
            Assert.Contains("{x}", p.UrlTemplate);
            Assert.Contains("{y}", p.UrlTemplate);
            Assert.Contains("{z}", p.UrlTemplate);
            // No personal keys: only the public shared key of IGN for its SCAN maps is allowed (ADR-0004).
            Assert.DoesNotContain("apikey", p.UrlTemplate.Replace("apikey=" + MapProviderCatalog.IgnScanPublicKey, string.Empty), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(p.UrlTemplate.Contains("{s}"), p.ServerNodes is { Count: > 0 });
        });
    }

    [Theory]
    [Trait("Req", "MAP-04")]
    [InlineData("opentopomap", "opentopomap")]
    [InlineData("thunderforest", "ign-es")] // removed provider falls back to the default
    [InlineData(null, "ign-es")]
    public void Unknown_ids_fall_back_to_the_default(string? id, string expected)
    {
        Assert.Equal(expected, MapProviderCatalog.FindOrDefault(id).Id);
    }

    [Fact]
    [Trait("Req", "MAP-02")]
    public void No_tile_url_template_exists_outside_the_catalog()
    {
        var root = Path.Combine(Fixture.RepositoryRoot(), "src");
        var offenders = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".xaml", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !f.EndsWith("MapProviderCatalog.cs", StringComparison.Ordinal))
            .Where(f => TileTemplate().IsMatch(File.ReadAllText(f)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    [GeneratedRegex(@"https?://[^""\s]*(\{z\}|TILEMATRIX)", RegexOptions.IgnoreCase)]
    private static partial Regex TileTemplate();
}
