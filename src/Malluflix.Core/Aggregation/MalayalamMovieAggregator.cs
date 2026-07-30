using System.Globalization;
using System.Text.RegularExpressions;
using Malluflix.Core.Bridge;
using Malluflix.Core.Configuration;
using Malluflix.Core.Models;
using Malluflix.Core.Models.Stremio;
using Malluflix.Core.Models.Tmdb;
using Malluflix.Core.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Malluflix.Core.Aggregation;

public sealed partial class MalayalamMovieAggregator(
    ITmdbClient tmdb,
    ICinemetaClient cinemeta,
    ITmdbImdbBridge bridge,
    IOptions<TmdbOptions> tmdbOptions,
    IOptions<CatalogOptions> catalogOptions,
    TimeProvider clock,
    ILogger<MalayalamMovieAggregator> logger) : IMalayalamMovieAggregator
{
    private const string TmdbReleaseTypeTheatrical = "3";
    private const string TmdbReleaseTypeDigitalOrPhysical = "4|5";
    private const int SearchMaxPages = 3;

    [GeneratedRegex(@"^tt\d{5,10}$")]
    private static partial Regex ImdbIdRegex();

    private readonly TmdbOptions _tmdb = tmdbOptions.Value;
    private readonly CatalogOptions _catalogs = catalogOptions.Value;

    public Task<IReadOnlyList<NormalizedMovie>> GetTheatersAsync(int skip, CancellationToken cancellationToken) =>
        DiscoverAsync(
            skip,
            releaseType: TmdbReleaseTypeTheatrical,
            windowStart: null,
            windowEnd: Today(),
            dateField: "primary_release_date",
            sort: "primary_release_date.desc",
            genreId: null,
            markInTheaters: true,
            markOnOtt: false,
            cancellationToken: cancellationToken);

    public Task<IReadOnlyList<NormalizedMovie>> GetOttAsync(int skip, CancellationToken cancellationToken) =>
        DiscoverAsync(
            skip,
            releaseType: TmdbReleaseTypeDigitalOrPhysical,
            windowStart: null,
            windowEnd: Today(),
            dateField: "release_date",
            sort: "release_date.desc",
            genreId: null,
            markInTheaters: false,
            markOnOtt: true,
            cancellationToken: cancellationToken);

    public Task<IReadOnlyList<NormalizedMovie>> GetUpcomingAsync(int skip, CancellationToken cancellationToken) =>
        DiscoverAsync(
            skip,
            releaseType: null,
            windowStart: Today().AddDays(1),
            windowEnd: Today().AddYears(1),
            dateField: "primary_release_date",
            sort: "primary_release_date.asc",
            genreId: null,
            markInTheaters: false,
            markOnOtt: false,
            cancellationToken: cancellationToken);

    public Task<IReadOnlyList<NormalizedMovie>> GetByGenreAsync(int tmdbGenreId, int skip, CancellationToken cancellationToken) =>
        DiscoverAsync(
            skip,
            releaseType: null,
            windowStart: null,
            windowEnd: Today(),
            dateField: "primary_release_date",
            sort: "primary_release_date.desc",
            genreId: tmdbGenreId,
            markInTheaters: false,
            markOnOtt: false,
            cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<NormalizedMovie>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        // TMDB search/movie doesn't accept with_original_language, so fan out a few pages and filter locally.
        var pageTasks = Enumerable.Range(1, SearchMaxPages)
            .Select(page => tmdb.SearchMoviesAsync(query, page, cancellationToken))
            .ToArray();
        var pages = await Task.WhenAll(pageTasks);

        var candidates = pages
            .Where(p => p is not null)
            .SelectMany(p => p!.Results)
            .Where(m => string.Equals(m.OriginalLanguage, _tmdb.OriginalLanguage, StringComparison.OrdinalIgnoreCase))
            .Take(_catalogs.PageSize)
            .ToArray();

        var bridged = await bridge.BridgeAsync(candidates, cancellationToken);
        return bridged.Select(pair => Normalize(pair.Movie, pair.ImdbId, isInTheaters: false, isOnOtt: false)).ToArray();
    }

    public async Task<StremioMeta?> GetMetaAsync(string imdbId, CancellationToken cancellationToken)
    {
        if (!IsValidImdbId(imdbId))
        {
            return null;
        }

        var cine = await cinemeta.GetMovieMetaAsync(imdbId, cancellationToken);
        if (cine is not null)
        {
            return cine;
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Cinemeta miss for {ImdbId}; falling back to TMDB", imdbId);
        }

        var find = await tmdb.FindByImdbIdAsync(imdbId, cancellationToken);
        var tmdbId = FirstMovie(find)?.Id;
        if (tmdbId is null)
        {
            return null;
        }

        var details = await tmdb.GetMovieDetailsAsync(tmdbId.Value, cancellationToken);
        return details is null ? null : ToStremioMeta(details, imdbId);
    }

    public async Task<NormalizedMovie?> GetNormalizedAsync(string imdbId, CancellationToken cancellationToken)
    {
        if (!IsValidImdbId(imdbId))
        {
            return null;
        }

        var find = await tmdb.FindByImdbIdAsync(imdbId, cancellationToken);
        var summary = FirstMovie(find);
        if (summary is null)
        {
            return null;
        }

        var details = await tmdb.GetMovieDetailsAsync(summary.Id, cancellationToken);
        if (details is null)
        {
            return null;
        }

        return new NormalizedMovie(
            ImdbId: imdbId,
            TmdbId: details.Id,
            Title: details.Title ?? summary.Title ?? string.Empty,
            Overview: details.Overview ?? summary.Overview,
            PosterUrl: BuildImage(details.PosterPath ?? summary.PosterPath, _tmdb.PosterSize),
            BackdropUrl: BuildImage(details.BackdropPath ?? summary.BackdropPath, _tmdb.BackdropSize),
            ReleaseDate: ParseDate(details.ReleaseDate ?? summary.ReleaseDate),
            RuntimeMinutes: details.Runtime,
            Genres: details.Genres?.Select(g => g.Name).ToArray() ?? [],
            Rating: details.VoteAverage ?? summary.VoteAverage,
            IsInTheaters: false,
            IsOnOtt: false);
    }

    private async Task<IReadOnlyList<NormalizedMovie>> DiscoverAsync(
        int skip,
        string? releaseType,
        DateOnly? windowStart,
        DateOnly? windowEnd,
        string dateField,
        string sort,
        int? genreId,
        bool markInTheaters,
        bool markOnOtt,
        CancellationToken cancellationToken)
    {
        var pageSize = Math.Max(1, _catalogs.PageSize);
        var upstreamPageSize = Math.Max(1, _tmdb.UpstreamPageSize);
        var startingPage = (skip / upstreamPageSize) + 1;
        var localSkip = skip % upstreamPageSize;

        var pagesNeeded = (int)Math.Ceiling((localSkip + pageSize) / (double)upstreamPageSize);
        pagesNeeded = Math.Max(pagesNeeded, 1);

        var pageTasks = Enumerable.Range(0, pagesNeeded)
            .Select(offset => tmdb.DiscoverAsync(
                BuildDiscoverQuery(startingPage + offset, releaseType, windowStart, windowEnd, dateField, sort, genreId),
                cancellationToken))
            .ToArray();
        var pages = await Task.WhenAll(pageTasks);

        var candidates = pages
            .Where(p => p is not null)
            .SelectMany(p => p!.Results)
            .Skip(localSkip)
            .Take(pageSize)
            .ToArray();

        var bridged = await bridge.BridgeAsync(candidates, cancellationToken);
        return bridged
            .Select(pair => Normalize(pair.Movie, pair.ImdbId, markInTheaters, markOnOtt))
            .ToArray();
    }

    private Dictionary<string, string?> BuildDiscoverQuery(
        int page,
        string? releaseType,
        DateOnly? windowStart,
        DateOnly? windowEnd,
        string dateField,
        string sort,
        int? genreId)
    {
        var query = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["with_original_language"] = _tmdb.OriginalLanguage,
            ["region"] = _tmdb.Region,
            ["sort_by"] = sort,
            ["page"] = page.ToString(CultureInfo.InvariantCulture),
            ["include_adult"] = "false",
        };

        if (!string.IsNullOrEmpty(releaseType))
        {
            query["with_release_type"] = releaseType;
        }

        if (windowStart is DateOnly start)
        {
            query[$"{dateField}.gte"] = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (windowEnd is DateOnly end)
        {
            query[$"{dateField}.lte"] = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (genreId is int gid)
        {
            query["with_genres"] = gid.ToString(CultureInfo.InvariantCulture);
        }

        return query;
    }

    private NormalizedMovie Normalize(TmdbMovie movie, string imdbId, bool isInTheaters, bool isOnOtt) =>
        new(
            ImdbId: imdbId,
            TmdbId: movie.Id,
            Title: movie.Title ?? movie.OriginalTitle ?? string.Empty,
            Overview: movie.Overview,
            PosterUrl: BuildImage(movie.PosterPath, _tmdb.PosterSize),
            BackdropUrl: BuildImage(movie.BackdropPath, _tmdb.BackdropSize),
            ReleaseDate: ParseDate(movie.ReleaseDate),
            RuntimeMinutes: null,
            Genres: [],
            Rating: movie.VoteAverage,
            IsInTheaters: isInTheaters,
            IsOnOtt: isOnOtt);

    private StremioMeta ToStremioMeta(TmdbMovieDetails details, string imdbId) =>
        new(
            Id: imdbId,
            Type: "movie",
            Name: details.Title ?? string.Empty,
            Poster: BuildImage(details.PosterPath, _tmdb.PosterSize),
            Background: BuildImage(details.BackdropPath, _tmdb.BackdropSize),
            Description: details.Overview,
            ReleaseInfo: ParseDate(details.ReleaseDate)?.Year.ToString(CultureInfo.InvariantCulture),
            Runtime: details.Runtime is int r ? $"{r} min" : null,
            ImdbRating: details.VoteAverage?.ToString("0.0", CultureInfo.InvariantCulture),
            Genres: details.Genres?.Select(g => g.Name).ToArray() ?? []);

    private string? BuildImage(string? path, string size) =>
        string.IsNullOrEmpty(path) ? null : $"{_tmdb.ImageBaseUrl}/{size}{path}";

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    private static TmdbMovie? FirstMovie(TmdbFindResponse? find) =>
        find?.MovieResults is { Count: > 0 } list ? list[0] : null;

    private static bool IsValidImdbId(string? imdbId) =>
        !string.IsNullOrEmpty(imdbId) && ImdbIdRegex().IsMatch(imdbId);
}
