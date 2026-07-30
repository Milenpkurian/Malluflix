using Malluflix.Core.Models.Tmdb;

namespace Malluflix.Core.Providers;

public interface ITmdbClient
{
    Task<TmdbDiscoverResponse?> DiscoverAsync(
        IReadOnlyDictionary<string, string?> discoverQuery,
        CancellationToken cancellationToken);

    Task<TmdbExternalIds?> GetExternalIdsAsync(int tmdbId, CancellationToken cancellationToken);

    Task<TmdbDiscoverResponse?> SearchMoviesAsync(string query, int page, CancellationToken cancellationToken);

    Task<TmdbMovieDetails?> GetMovieDetailsAsync(int tmdbId, CancellationToken cancellationToken);

    Task<TmdbFindResponse?> FindByImdbIdAsync(string imdbId, CancellationToken cancellationToken);
}
