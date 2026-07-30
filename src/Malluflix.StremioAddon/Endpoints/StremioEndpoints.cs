using System.Globalization;
using System.Text.RegularExpressions;
using Malluflix.Core.Aggregation;
using Malluflix.Core.Configuration;
using Malluflix.Core.Models;
using Malluflix.Core.Models.Stremio;
using Malluflix.StremioAddon.Manifest;
using Microsoft.Extensions.Options;

namespace Malluflix.StremioAddon.Endpoints;

public static partial class StremioEndpoints
{
    [GeneratedRegex(@"^tt\d{5,10}$")]
    private static partial Regex ImdbIdRegex();

    public static void MapStremio(this IEndpointRouteBuilder app)
    {
        app.MapGet("/manifest.json", (IManifestBuilder builder) => Results.Ok(builder.Build()))
            .WithTags("Stremio");

        app.MapGet("/catalog/{type}/{id}.json",
            (string type, string id, string? genre,
                IMalayalamMovieAggregator aggregator,
                IOptionsMonitor<CatalogOptions> catalogOptions,
                CancellationToken ct) =>
                    HandleCatalog(type, id, skip: 0, search: null, aggregator, catalogOptions, ct))
            .WithTags("Stremio");

        app.MapGet("/catalog/{type}/{id}/skip={skip:int}.json",
            (string type, string id, int skip,
                IMalayalamMovieAggregator aggregator,
                IOptionsMonitor<CatalogOptions> catalogOptions,
                CancellationToken ct) =>
                    HandleCatalog(type, id, Math.Max(skip, 0), search: null, aggregator, catalogOptions, ct))
            .WithTags("Stremio");

        app.MapGet("/catalog/{type}/{id}/search={search}.json",
            (string type, string id, string search,
                IMalayalamMovieAggregator aggregator,
                IOptionsMonitor<CatalogOptions> catalogOptions,
                CancellationToken ct) =>
                    HandleCatalog(type, id, skip: 0, search, aggregator, catalogOptions, ct))
            .WithTags("Stremio");

        app.MapGet("/meta/{type}/{id}.json",
            async (string type, string id,
                IMalayalamMovieAggregator aggregator,
                CancellationToken ct) =>
        {
            if (!string.Equals(type, "movie", StringComparison.Ordinal) || !ImdbIdRegex().IsMatch(id))
            {
                return Results.Ok(new StremioMetaResponse(null));
            }

            var meta = await aggregator.GetMetaAsync(id, ct);
            return Results.Ok(new StremioMetaResponse(meta));
        })
        .WithTags("Stremio");
    }

    private static async Task<IResult> HandleCatalog(
        string type,
        string id,
        int skip,
        string? search,
        IMalayalamMovieAggregator aggregator,
        IOptionsMonitor<CatalogOptions> catalogOptions,
        CancellationToken ct)
    {
        if (!string.Equals(type, "movie", StringComparison.Ordinal))
        {
            return EmptyCatalog();
        }

        var descriptor = catalogOptions.CurrentValue.BuildCatalogs()
            .FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal));

        if (descriptor is null)
        {
            return EmptyCatalog();
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            if (!descriptor.SupportsSearch)
            {
                return EmptyCatalog();
            }

            var searchResults = await aggregator.SearchAsync(search, ct);
            return Results.Ok(new StremioCatalogResponse(searchResults.Select(ToCatalogItem).ToArray()));
        }

        var items = descriptor.Kind switch
        {
            CatalogKind.Theaters => await aggregator.GetTheatersAsync(skip, ct),
            CatalogKind.Ott => await aggregator.GetOttAsync(skip, ct),
            CatalogKind.Upcoming => await aggregator.GetUpcomingAsync(skip, ct),
            CatalogKind.Genre when descriptor.TmdbGenreId is int gid =>
                await aggregator.GetByGenreAsync(gid, skip, ct),
            _ => (IReadOnlyList<NormalizedMovie>)[],
        };

        return Results.Ok(new StremioCatalogResponse(items.Select(ToCatalogItem).ToArray()));
    }

    private static IResult EmptyCatalog() =>
        Results.Ok(new StremioCatalogResponse([]));

    private static StremioCatalogItem ToCatalogItem(NormalizedMovie movie) =>
        new(
            Id: movie.ImdbId,
            Type: "movie",
            Name: movie.Title,
            Poster: movie.PosterUrl,
            PosterShape: "poster",
            Background: movie.BackdropUrl,
            Description: movie.Overview,
            ReleaseInfo: movie.ReleaseDate?.Year.ToString(CultureInfo.InvariantCulture),
            ImdbRating: movie.Rating?.ToString("0.0", CultureInfo.InvariantCulture),
            Genres: movie.Genres);
}
