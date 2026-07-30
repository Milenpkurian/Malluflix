using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Malluflix.Core.Caching;

/// <summary>
/// Default <see cref="ICachedHttpJson"/> backed by <see cref="IDistributedCache"/>.
/// Wiring <c>AddDistributedMemoryCache()</c> keeps everything in-process today;
/// swap to <c>AddStackExchangeRedisCache</c> without touching a single call site.
/// </summary>
public sealed class CachedHttpJson(
    IDistributedCache cache,
    ILogger<CachedHttpJson> logger) : ICachedHttpJson
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    // Query-string parameter names that must be stripped from cache keys and log output.
    private static readonly HashSet<string> SensitiveQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "api_key",
        "access_token",
    };

    // Sentinel body for negatively-cached 404 responses.
    private static readonly byte[] NotFoundMarker = [0xFF];

    private static readonly TimeSpan NegativeTtl = TimeSpan.FromMinutes(1);

    public async Task<T?> GetAsync<T>(
        HttpClient client,
        string relativeOrAbsoluteUrl,
        IReadOnlyDictionary<string, string?>? query,
        TimeSpan ttl,
        CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeOrAbsoluteUrl);

        var fullUrl = BuildAbsoluteUrl(client, relativeOrAbsoluteUrl, query, includeSensitive: true);
        var safeUrl = BuildAbsoluteUrl(client, relativeOrAbsoluteUrl, query, includeSensitive: false);
        var cacheKey = BuildCacheKey(safeUrl);

        var cached = await cache.GetAsync(cacheKey, cancellationToken);
        if (cached is { Length: > 0 })
        {
            if (IsNotFoundMarker(cached))
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("Negative cache hit for: {Url}", safeUrl);
                }

                return null;
            }

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Cache hit for: {Url}", safeUrl);
            }

            try
            {
                return JsonSerializer.Deserialize<T>(cached, JsonOptions);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Corrupt cache entry for {Url}; refetching", safeUrl);
            }
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Cache miss for: {Url}", safeUrl);
        }

        using var response = await client.GetAsync(fullUrl, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            await cache.SetAsync(
                cacheKey,
                NotFoundMarker,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = NegativeTtl },
                cancellationToken);
            return null;
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        await cache.SetAsync(
            cacheKey,
            payload,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
            cancellationToken);

        return JsonSerializer.Deserialize<T>(payload, JsonOptions);
    }

    private static Uri BuildAbsoluteUrl(
        HttpClient client,
        string relativeOrAbsoluteUrl,
        IReadOnlyDictionary<string, string?>? query,
        bool includeSensitive)
    {
        var baseUri = Uri.TryCreate(relativeOrAbsoluteUrl, UriKind.Absolute, out var absolute)
            ? absolute
            : new Uri(client.BaseAddress ?? throw new InvalidOperationException(
                    "HttpClient.BaseAddress must be set when the URL is relative."),
                relativeOrAbsoluteUrl);

        if (query is null || query.Count == 0)
        {
            return baseUri;
        }

        var builder = new UriBuilder(baseUri);
        var sortedPairs = query
            .Where(kvp => !string.IsNullOrEmpty(kvp.Value))
            .Where(kvp => includeSensitive || !SensitiveQueryKeys.Contains(kvp.Key))
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal);

        var sb = new StringBuilder();
        foreach (var kvp in sortedPairs)
        {
            if (sb.Length > 0)
            {
                sb.Append('&');
            }

            sb.Append(Uri.EscapeDataString(kvp.Key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(kvp.Value!));
        }

        builder.Query = sb.ToString();
        return builder.Uri;
    }

    private static string BuildCacheKey(Uri safeUrl) => $"malluflix::http::{safeUrl}";

    private static bool IsNotFoundMarker(byte[] cached) =>
        cached.Length == NotFoundMarker.Length && cached[0] == NotFoundMarker[0];
}
