using System.Net;
using Malluflix.Core.Aggregation;
using Malluflix.Core.Bridge;
using Malluflix.Core.Caching;
using Malluflix.Core.Configuration;
using Malluflix.Core.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Malluflix.Core.DependencyInjection;

public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the aggregator core: options, cache, typed HTTP clients (with resilience),
    /// providers, ID bridge, and the top-level <see cref="IMalayalamMovieAggregator"/>.
    /// </summary>
    public static IServiceCollection AddMalluflixCore(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<TmdbOptions>()
            .Bind(configuration.GetSection(TmdbOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), "Tmdb:ApiKey is required.")
            .Validate(o => Uri.IsWellFormedUriString(o.BaseUrl, UriKind.Absolute), "Tmdb:BaseUrl must be absolute.")
            .ValidateOnStart();

        services.AddOptions<CatalogOptions>()
            .Bind(configuration.GetSection(CatalogOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<CinemetaOptions>()
            .Bind(configuration.GetSection(CinemetaOptions.SectionName))
            .Validate(o => Uri.IsWellFormedUriString(o.BaseUrl, UriKind.Absolute), "Cinemeta:BaseUrl must be absolute.")
            .Validate(o => o.TimeoutSeconds is > 0 and <= 60, "Cinemeta:TimeoutSeconds must be between 1 and 60.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        services.AddDistributedMemoryCache();
        services.AddSingleton<ICachedHttpJson, CachedHttpJson>();

        services.AddHttpClient<ITmdbClient, TmdbClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<TmdbOptions>>().Value;
            var baseUrl = opts.BaseUrl.EndsWith('/') ? opts.BaseUrl : opts.BaseUrl + "/";
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.Timeout = TimeSpan.FromSeconds(15);
        })
        .AddStandardResilienceHandler(ConfigureResilience);

        services.AddHttpClient<ICinemetaClient, CinemetaClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<CinemetaOptions>>().Value;
            var baseUrl = opts.BaseUrl.EndsWith('/') ? opts.BaseUrl : opts.BaseUrl + "/";
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        })
        .AddStandardResilienceHandler(ConfigureResilience);

        services.AddSingleton<ITmdbImdbBridge, TmdbImdbBridge>();
        services.AddSingleton<IMalayalamMovieAggregator, MalayalamMovieAggregator>();

        return services;
    }

    private static void ConfigureResilience(HttpStandardResilienceOptions options)
    {
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.UseJitter = true;
        options.Retry.Delay = TimeSpan.FromMilliseconds(400);

        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(45);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    }
}
