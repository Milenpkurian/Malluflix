using Malluflix.Core.Bridge;
using Malluflix.Core.Models.Tmdb;
using Malluflix.Core.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Malluflix.Tests.Bridge;

public class TmdbImdbBridgeTests
{
    private static TmdbMovie Movie(int id) =>
        new(Id: id, Title: $"m{id}", OriginalTitle: null, OriginalLanguage: "ml",
            Overview: null, PosterPath: null, BackdropPath: null,
            ReleaseDate: null, VoteAverage: null, GenreIds: null);

    [Fact]
    public async Task Drops_items_without_imdb_id()
    {
        var tmdb = new Mock<ITmdbClient>();
        tmdb.Setup(x => x.GetExternalIdsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbExternalIds(1, "tt0000001"));
        tmdb.Setup(x => x.GetExternalIdsAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbExternalIds(2, null));
        tmdb.Setup(x => x.GetExternalIdsAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbExternalIds(3, "tt0000003"));

        var sut = new TmdbImdbBridge(tmdb.Object, NullLogger<TmdbImdbBridge>.Instance);
        var result = await sut.BridgeAsync([Movie(1), Movie(2), Movie(3)], CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(r => r.ImdbId).Should().BeEquivalentTo(["tt0000001", "tt0000003"]);
    }

    [Fact]
    public async Task Concurrent_calls_stay_within_semaphore_cap()
    {
        var maxObserved = 0;
        var current = 0;
        var lockObj = new object();

        var tmdb = new Mock<ITmdbClient>();
        tmdb.Setup(x => x.GetExternalIdsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<int, CancellationToken>(async (id, ct) =>
            {
                lock (lockObj)
                {
                    current++;
                    if (current > maxObserved) maxObserved = current;
                }

                await Task.Delay(10, ct);

                lock (lockObj) { current--; }
                return new TmdbExternalIds(id, $"tt{id:0000000}");
            });

        var sut = new TmdbImdbBridge(tmdb.Object, NullLogger<TmdbImdbBridge>.Instance);
        var movies = Enumerable.Range(1, 25).Select(Movie).ToArray();

        _ = await sut.BridgeAsync(movies, CancellationToken.None);

        maxObserved.Should().BeLessThanOrEqualTo(TmdbImdbBridge.MaxConcurrency);
    }

    [Fact]
    public async Task Http_exception_drops_item_instead_of_throwing()
    {
        var tmdb = new Mock<ITmdbClient>();
        tmdb.Setup(x => x.GetExternalIdsAsync(1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("nope"));
        tmdb.Setup(x => x.GetExternalIdsAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbExternalIds(2, "tt0000002"));

        var sut = new TmdbImdbBridge(tmdb.Object, NullLogger<TmdbImdbBridge>.Instance);
        var result = await sut.BridgeAsync([Movie(1), Movie(2)], CancellationToken.None);

        result.Should().ContainSingle().Which.ImdbId.Should().Be("tt0000002");
    }
}
