using Malluflix.Core.Models.Tmdb;
using Malluflix.Core.Providers;
using Microsoft.Extensions.Logging;

namespace Malluflix.Core.Bridge;

public interface ITmdbImdbBridge
{
    /// <summary>
    /// Enriches each TMDB movie with its IMDb id, dropping items whose bridge lookup fails.
    /// Runs at most <see cref="MaxConcurrency"/> upstream calls at a time to respect TMDB rate limits.
    /// </summary>
    Task<IReadOnlyList<(TmdbMovie Movie, string ImdbId)>> BridgeAsync(
        IEnumerable<TmdbMovie> movies,
        CancellationToken cancellationToken);
}

public sealed class TmdbImdbBridge(
    ITmdbClient tmdb,
    ILogger<TmdbImdbBridge> logger) : ITmdbImdbBridge
{
    public const int MaxConcurrency = 5;

    public async Task<IReadOnlyList<(TmdbMovie Movie, string ImdbId)>> BridgeAsync(
        IEnumerable<TmdbMovie> movies,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(movies);

        using var gate = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
        var tasks = movies.Select(async movie =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var external = await tmdb.GetExternalIdsAsync(movie.Id, cancellationToken);
                var imdb = external?.ImdbId;
                if (string.IsNullOrEmpty(imdb) || !imdb.StartsWith("tt", StringComparison.Ordinal))
                {
                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug("No IMDb id for TMDB {TmdbId}; dropping", movie.Id);
                    }

                    return ((TmdbMovie Movie, string ImdbId)?)null;
                }

                return (movie, imdb);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "external_ids failed for TMDB {TmdbId}; dropping", movie.Id);
                return null;
            }
            finally
            {
                gate.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        return results
            .Where(r => r.HasValue)
            .Select(r => r!.Value)
            .ToArray();
    }
}
