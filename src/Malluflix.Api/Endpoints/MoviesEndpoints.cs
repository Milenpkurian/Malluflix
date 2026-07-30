using System.Text.RegularExpressions;
using Malluflix.Core.Aggregation;
using Malluflix.Core.Configuration;
using Malluflix.Core.Models;
using Malluflix.Core.Models.Stremio;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Malluflix.Api.Endpoints;

public static partial class MoviesEndpoints
{
    [GeneratedRegex(@"^tt\d{5,10}$")]
    private static partial Regex ImdbIdRegex();

    public static void MapMovies(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/movies")
            .WithTags("Movies")
            .RequireRateLimiting("api");

        group.MapGet("/theaters", async (
            int? skip,
            IMalayalamMovieAggregator aggregator,
            CancellationToken ct) =>
                Results.Ok(await aggregator.GetTheatersAsync(NonNegative(skip), ct)))
            .WithSummary("Latest Malayalam movies currently in theaters (IN)");

        group.MapGet("/ott", async (
            int? skip,
            IMalayalamMovieAggregator aggregator,
            CancellationToken ct) =>
                Results.Ok(await aggregator.GetOttAsync(NonNegative(skip), ct)))
            .WithSummary("Latest Malayalam movies available on OTT platforms (IN)");

        group.MapGet("/upcoming", async (
            int? skip,
            IMalayalamMovieAggregator aggregator,
            CancellationToken ct) =>
                Results.Ok(await aggregator.GetUpcomingAsync(NonNegative(skip), ct)))
            .WithSummary("Upcoming Malayalam theatrical releases");

        group.MapGet("/genre/{tmdbGenreId:int}", async (
            int tmdbGenreId,
            int? skip,
            IMalayalamMovieAggregator aggregator,
            IOptionsMonitor<CatalogOptions> catalogOptions,
            CancellationToken ct) =>
        {
            var known = catalogOptions.CurrentValue.EffectiveGenres.ContainsKey(tmdbGenreId);
            if (!known)
            {
                return Results.NotFound(new { message = $"Unknown TMDB genre id {tmdbGenreId}." });
            }

            var items = await aggregator.GetByGenreAsync(tmdbGenreId, NonNegative(skip), ct);
            return Results.Ok(items);
        })
        .WithSummary("Latest Malayalam movies for a given TMDB genre id");

        group.MapGet("/search", async (
            string q,
            IMalayalamMovieAggregator aggregator,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest(new { message = "Query 'q' is required." });
            }

            var results = await aggregator.SearchAsync(q, ct);
            return Results.Ok(results);
        })
        .WithSummary("Search Malayalam movies by title");

        group.MapGet("/{imdbId}", async (
            string imdbId,
            IMalayalamMovieAggregator aggregator,
            CancellationToken ct) =>
        {
            if (!ImdbIdRegex().IsMatch(imdbId))
            {
                return Results.BadRequest(new { message = "imdbId must match tt#####." });
            }

            var movie = await aggregator.GetNormalizedAsync(imdbId, ct);
            return movie is null ? Results.NotFound() : Results.Ok(movie);
        })
        .WithSummary("Movie details by IMDb id");

        app.MapGet("/api/genres", (IOptionsMonitor<CatalogOptions> catalogOptions) =>
                Results.Ok(catalogOptions.CurrentValue.EffectiveGenres))
            .WithTags("Movies")
            .WithSummary("Available TMDB genre ids for genre-scoped queries");
    }

    private static int NonNegative(int? skip) => skip is int s && s > 0 ? s : 0;
}
