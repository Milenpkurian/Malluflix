using Malluflix.Core.Caching;
using Malluflix.Core.Configuration;
using Malluflix.Core.Models.Tmdb;
using Microsoft.Extensions.Options;

namespace Malluflix.Core.Providers;

public sealed class TmdbClient(
    HttpClient httpClient,
    ICachedHttpJson cache,
    IOptions<TmdbOptions> options) : ITmdbClient
{
    private readonly TmdbOptions _options = options.Value;

    public Task<TmdbDiscoverResponse?> DiscoverAsync(
        IReadOnlyDictionary<string, string?> discoverQuery,
        CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string?>(discoverQuery, StringComparer.Ordinal)
        {
            ["api_key"] = _options.ApiKey,
        };
        return cache.GetAsync<TmdbDiscoverResponse>(
            httpClient,
            "discover/movie",
            query,
            ICachedHttpJson.CatalogTtl,
            cancellationToken);
    }

    public Task<TmdbExternalIds?> GetExternalIdsAsync(int tmdbId, CancellationToken cancellationToken) =>
        cache.GetAsync<TmdbExternalIds>(
            httpClient,
            $"movie/{tmdbId}/external_ids",
            new Dictionary<string, string?> { ["api_key"] = _options.ApiKey },
            ICachedHttpJson.CatalogTtl,
            cancellationToken);

    public Task<TmdbDiscoverResponse?> SearchMoviesAsync(string query, int page, CancellationToken cancellationToken) =>
        cache.GetAsync<TmdbDiscoverResponse>(
            httpClient,
            "search/movie",
            new Dictionary<string, string?>
            {
                ["api_key"] = _options.ApiKey,
                ["query"] = query,
                ["page"] = page.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["include_adult"] = "false",
                ["language"] = "en-US",
                ["region"] = _options.Region,
            },
            ICachedHttpJson.SearchTtl,
            cancellationToken);

    public Task<TmdbMovieDetails?> GetMovieDetailsAsync(int tmdbId, CancellationToken cancellationToken) =>
        cache.GetAsync<TmdbMovieDetails>(
            httpClient,
            $"movie/{tmdbId}",
            new Dictionary<string, string?> { ["api_key"] = _options.ApiKey },
            ICachedHttpJson.MetaTtl,
            cancellationToken);

    public Task<TmdbFindResponse?> FindByImdbIdAsync(string imdbId, CancellationToken cancellationToken) =>
        cache.GetAsync<TmdbFindResponse>(
            httpClient,
            $"find/{imdbId}",
            new Dictionary<string, string?>
            {
                ["api_key"] = _options.ApiKey,
                ["external_source"] = "imdb_id",
            },
            ICachedHttpJson.MetaTtl,
            cancellationToken);
}
