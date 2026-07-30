using System.Text.Json;

namespace Malluflix.Core.Caching;

/// <summary>
/// Fetches JSON from an upstream URL through a shared cache keyed on <c>url + sorted(query)</c>.
/// Matches the MalluFlix <c>fetchWithCache</c> primitive; TTL is per-call so callers pick 24h/1h/5m.
/// </summary>
public interface ICachedHttpJson
{
    Task<T?> GetAsync<T>(
        HttpClient client,
        string relativeOrAbsoluteUrl,
        IReadOnlyDictionary<string, string?>? query,
        TimeSpan ttl,
        CancellationToken cancellationToken)
        where T : class;

    /// <summary>TTL used for the primary catalog listings (discover, search).</summary>
    static readonly TimeSpan CatalogTtl = TimeSpan.FromHours(24);

    /// <summary>TTL used for meta lookups (Cinemeta, TMDB movie details).</summary>
    static readonly TimeSpan MetaTtl = TimeSpan.FromHours(1);

    /// <summary>TTL used for user-triggered search calls.</summary>
    static readonly TimeSpan SearchTtl = TimeSpan.FromMinutes(5);
}
