using Malluflix.Core.Caching;
using Malluflix.Core.Models.Stremio;
using Microsoft.Extensions.Logging;

namespace Malluflix.Core.Providers;

public interface ICinemetaClient
{
    Task<StremioMeta?> GetMovieMetaAsync(string imdbId, CancellationToken cancellationToken);
}

public sealed class CinemetaClient(
    HttpClient httpClient,
    ICachedHttpJson cache,
    ILogger<CinemetaClient> logger) : ICinemetaClient
{
    public async Task<StremioMeta?> GetMovieMetaAsync(string imdbId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await cache.GetAsync<StremioMetaResponse>(
                httpClient,
                $"meta/movie/{imdbId}.json",
                query: null,
                ICachedHttpJson.MetaTtl,
                cancellationToken);
            return response?.Meta;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Cinemeta lookup failed for {ImdbId}", imdbId);
            return null;
        }
    }
}
