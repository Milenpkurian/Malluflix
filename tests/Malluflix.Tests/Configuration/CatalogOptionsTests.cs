using Malluflix.Core.Configuration;

namespace Malluflix.Tests.Configuration;

public class CatalogOptionsTests
{
    [Fact]
    public void Default_uses_15_seeded_genres()
    {
        var opts = new CatalogOptions();

        opts.EffectiveGenres.Should().HaveCount(15);
        opts.EffectiveGenres.Keys.Should().Contain([28, 35, 53, 18, 10749]);
    }

    [Fact]
    public void Configured_genres_override_defaults()
    {
        var opts = new CatalogOptions
        {
            Genres = new Dictionary<int, string> { [28] = "Action" },
        };

        opts.EffectiveGenres.Should().HaveCount(1);
        opts.EffectiveGenres.Should().ContainKey(28);
    }

    [Fact]
    public void BuildCatalogs_marks_theaters_as_searchable()
    {
        var opts = new CatalogOptions { EnableGenres = false };

        var theaters = opts.BuildCatalogs().Single(c => c.Kind == CatalogKind.Theaters);

        theaters.SupportsSearch.Should().BeTrue();
    }

    [Fact]
    public void Genre_catalogs_get_prefixed_ids()
    {
        var opts = new CatalogOptions
        {
            EnableTheaters = false,
            EnableOtt = false,
            EnableUpcoming = false,
            Genres = new Dictionary<int, string> { [42] = "Custom" },
        };

        opts.BuildCatalogs()
            .Should().ContainSingle()
            .Which.Id.Should().Be("malayalam_genre_42");
    }
}
