using Malluflix.Core.Aggregation;
using Malluflix.Core.Bridge;
using Malluflix.Core.Configuration;
using Malluflix.Core.Providers;
using Malluflix.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Malluflix.Tests.Aggregation;

public class MalayalamMovieAggregatorTests
{
    private static (MalayalamMovieAggregator Sut, FakeHttpMessageHandler Handler) Build(FixedTimeProvider? clock = null)
    {
        var handler = new FakeHttpMessageHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/3/") };
        var cinemetaHttp = new HttpClient(handler) { BaseAddress = new Uri("https://v3-cinemeta.strem.io/") };
        var cache = TestCache.CreateInMemory();
        var tmdbOptions = Options.Create(new TmdbOptions { ApiKey = "TESTKEY" });
        var catalogOptions = Options.Create(new CatalogOptions());

        var tmdb = new TmdbClient(http, cache, tmdbOptions);
        var cinemeta = new CinemetaClient(cinemetaHttp, cache, NullLogger<CinemetaClient>.Instance);
        var bridge = new TmdbImdbBridge(tmdb, NullLogger<TmdbImdbBridge>.Instance);
        var sut = new MalayalamMovieAggregator(
            tmdb, cinemeta, bridge, tmdbOptions, catalogOptions,
            clock ?? new FixedTimeProvider(new DateTime(2026, 07, 30, 0, 0, 0, DateTimeKind.Utc)),
            NullLogger<MalayalamMovieAggregator>.Instance);

        return (sut, handler);
    }

    [Fact]
    public async Task GetTheatersAsync_calls_discover_with_release_type_3()
    {
        var (sut, handler) = Build();
        handler.RespondJson("discover/movie",
            """{ "page": 1, "total_pages": 1, "total_results": 0, "results": [] }""");

        _ = await sut.GetTheatersAsync(skip: 0, CancellationToken.None);

        var discover = handler.Requests.Single(r => r.RequestUri!.AbsolutePath.EndsWith("/discover/movie"));
        var qs = discover.RequestUri!.Query;
        qs.Should().Contain("with_original_language=ml");
        qs.Should().Contain("region=IN");
        qs.Should().Contain("with_release_type=3");
        qs.Should().Contain("api_key=TESTKEY");
    }

    [Fact]
    public async Task GetOttAsync_uses_release_type_4_or_5()
    {
        var (sut, handler) = Build();
        handler.RespondJson("discover/movie",
            """{ "page": 1, "total_pages": 1, "total_results": 0, "results": [] }""");

        _ = await sut.GetOttAsync(skip: 0, CancellationToken.None);

        var discover = handler.Requests.Single(r => r.RequestUri!.AbsolutePath.EndsWith("/discover/movie"));
        var qs = discover.RequestUri!.Query;
        qs.Should().Contain("with_release_type=4%7C5");
        qs.Should().Contain("sort_by=release_date.desc");
        qs.Should().Contain("release_date.lte=");
        qs.Should().NotContain("release_date.gte=");
    }

    [Fact]
    public async Task Discover_results_are_bridged_to_imdb_and_normalized()
    {
        var (sut, handler) = Build();
        handler.RespondJson("discover/movie", """
        {
            "page": 1, "total_pages": 1, "total_results": 2,
            "results": [
                { "id": 100, "title": "Aavesham", "original_language": "ml", "poster_path": "/p1.jpg", "release_date": "2026-04-11", "vote_average": 8.1 },
                { "id": 101, "title": "Manjummel Boys", "original_language": "ml", "poster_path": "/p2.jpg", "release_date": "2026-02-22", "vote_average": 8.6 }
            ]
        }
        """);
        handler.RespondJson("/movie/100/external_ids", """{ "id": 100, "imdb_id": "tt0000100" }""");
        handler.RespondJson("/movie/101/external_ids", """{ "id": 101, "imdb_id": "tt0000101" }""");

        var result = await sut.GetTheatersAsync(skip: 0, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(m => m.ImdbId).Should().BeEquivalentTo(["tt0000100", "tt0000101"]);
        result[0].IsInTheaters.Should().BeTrue();
        result[0].PosterUrl.Should().Contain("/p1.jpg");
    }

    [Fact]
    public async Task GetMetaAsync_returns_cinemeta_when_available()
    {
        var (sut, handler) = Build();
        handler.RespondJson("meta/movie/tt0000100.json", """
        { "meta": { "id": "tt0000100", "type": "movie", "name": "Aavesham", "description": "" } }
        """);

        var meta = await sut.GetMetaAsync("tt0000100", CancellationToken.None);

        meta.Should().NotBeNull();
        meta!.Name.Should().Be("Aavesham");
    }

    [Fact]
    public async Task GetMetaAsync_falls_back_to_tmdb_when_cinemeta_returns_null()
    {
        var (sut, handler) = Build();
        handler.RespondJson("meta/movie/tt0000100.json", """{ "meta": null }""");
        handler.RespondJson("find/tt0000100", """
        { "movie_results": [ { "id": 100, "original_language": "ml", "title": "Fallback Title" } ] }
        """);
        handler.RespondJson("/movie/100", """
        {
            "id": 100, "imdb_id": "tt0000100", "title": "Fallback Title",
            "release_date": "2026-04-11", "runtime": 145, "vote_average": 7.5,
            "genres": [ { "id": 28, "name": "Action" } ]
        }
        """);

        var meta = await sut.GetMetaAsync("tt0000100", CancellationToken.None);

        meta.Should().NotBeNull();
        meta!.Name.Should().Be("Fallback Title");
        meta.Runtime.Should().Be("145 min");
        meta.Genres.Should().Contain("Action");
    }
}
