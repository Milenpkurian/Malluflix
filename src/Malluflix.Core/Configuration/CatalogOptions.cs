namespace Malluflix.Core.Configuration;

public enum CatalogKind
{
    Theaters,
    Ott,
    Upcoming,
    Genre,
}

public sealed record CatalogDescriptor(
    string Id,
    string Name,
    CatalogKind Kind,
    int? TmdbGenreId = null,
    bool SupportsSearch = false);

/// <summary>Bound from configuration section <c>Catalogs</c>. Drives BOTH the Stremio manifest and REST routes.</summary>
public sealed class CatalogOptions
{
    public const string SectionName = "Catalogs";

    public string IdPrefix { get; set; } = "malayalam_";

    public bool EnableTheaters { get; set; } = true;

    public bool EnableOtt { get; set; } = true;

    public bool EnableUpcoming { get; set; } = true;

    public bool EnableGenres { get; set; } = true;

    public int PageSize { get; set; } = 20;

    /// <summary>Genre id (TMDB) → display name. Overrides <see cref="DefaultGenres"/> when populated.</summary>
    public Dictionary<int, string> Genres { get; set; } = new();

    /// <summary>Default TMDB genre map for Malayalam catalogs. Keys are official TMDB movie genre ids.</summary>
    public static readonly IReadOnlyDictionary<int, string> DefaultGenres = new Dictionary<int, string>
    {
        [28] = "Action",
        [12] = "Adventure",
        [16] = "Animation",
        [35] = "Comedy",
        [80] = "Crime",
        [99] = "Documentary",
        [18] = "Drama",
        [10751] = "Family",
        [14] = "Fantasy",
        [36] = "History",
        [27] = "Horror",
        [9648] = "Mystery",
        [10749] = "Romance",
        [878] = "Science Fiction",
        [53] = "Thriller",
    };

    public IReadOnlyDictionary<int, string> EffectiveGenres =>
        Genres.Count > 0 ? Genres : DefaultGenres;

    public IEnumerable<CatalogDescriptor> BuildCatalogs()
    {
        if (EnableTheaters)
        {
            yield return new CatalogDescriptor(
                Id: $"{IdPrefix}theaters",
                Name: "Malayalam · In Theaters",
                Kind: CatalogKind.Theaters,
                SupportsSearch: true);
        }

        if (EnableOtt)
        {
            yield return new CatalogDescriptor(
                Id: $"{IdPrefix}ott",
                Name: "Malayalam · On OTT",
                Kind: CatalogKind.Ott);
        }

        if (EnableUpcoming)
        {
            yield return new CatalogDescriptor(
                Id: $"{IdPrefix}upcoming",
                Name: "Malayalam · Coming Soon",
                Kind: CatalogKind.Upcoming);
        }

        if (EnableGenres)
        {
            foreach (var (id, name) in EffectiveGenres)
            {
                yield return new CatalogDescriptor(
                    Id: $"{IdPrefix}genre_{id}",
                    Name: $"Malayalam · {name}",
                    Kind: CatalogKind.Genre,
                    TmdbGenreId: id);
            }
        }
    }
}
