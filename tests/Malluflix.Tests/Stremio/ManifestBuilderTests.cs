using Malluflix.Core.Configuration;
using Malluflix.StremioAddon.Manifest;
using Malluflix.Tests.TestSupport;

namespace Malluflix.Tests.Stremio;

public class ManifestBuilderTests
{
    [Fact]
    public void Manifest_declares_catalog_meta_movie_and_tt_prefix()
    {
        var opts = StaticOptionsMonitor.For(new CatalogOptions());
        var sut = new ManifestBuilder(opts);

        var manifest = sut.Build();

        manifest.Resources.Should().BeEquivalentTo(["catalog", "meta"]);
        manifest.Types.Should().BeEquivalentTo(["movie"]);
        manifest.IdPrefixes.Should().BeEquivalentTo(["tt"]);
    }

    [Fact]
    public void Default_catalogs_include_theaters_ott_upcoming_and_all_genres()
    {
        var opts = StaticOptionsMonitor.For(new CatalogOptions());
        var sut = new ManifestBuilder(opts);

        var manifest = sut.Build();

        manifest.Catalogs.Should().Contain(c => c.Id == "malayalam_theaters");
        manifest.Catalogs.Should().Contain(c => c.Id == "malayalam_ott");
        manifest.Catalogs.Should().Contain(c => c.Id == "malayalam_upcoming");
        manifest.Catalogs.Should().HaveCount(3 + CatalogOptions.DefaultGenres.Count);
        manifest.Catalogs.All(c => c.Type == "movie").Should().BeTrue();
    }

    [Fact]
    public void Only_search_catalog_advertises_search_extra()
    {
        var opts = StaticOptionsMonitor.For(new CatalogOptions());
        var sut = new ManifestBuilder(opts);

        var manifest = sut.Build();

        var theaters = manifest.Catalogs.Single(c => c.Id == "malayalam_theaters");
        theaters.Extra.Should().Contain(e => e.Name == "search");

        var ott = manifest.Catalogs.Single(c => c.Id == "malayalam_ott");
        ott.Extra.Should().NotContain(e => e.Name == "search");
    }

    [Fact]
    public void Disabling_genres_shrinks_the_catalog_list()
    {
        var opts = StaticOptionsMonitor.For(new CatalogOptions { EnableGenres = false });
        var sut = new ManifestBuilder(opts);

        var manifest = sut.Build();

        manifest.Catalogs.Should().HaveCount(3);
    }
}
