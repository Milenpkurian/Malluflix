using System.Net;
using Malluflix.Core.Caching;
using Malluflix.Tests.TestSupport;

namespace Malluflix.Tests.Caching;

public class CachedHttpJsonTests
{
    private sealed record Sample(int Value);

    [Fact]
    public async Task Miss_then_hit_reuses_cached_bytes()
    {
        var handler = new FakeHttpMessageHandler();
        handler.RespondJson("api/v1/thing", """{ "value": 42 }""");

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var cache = TestCache.CreateInMemory();

        var query = new Dictionary<string, string?> { ["a"] = "1", ["b"] = "2" };
        var first = await cache.GetAsync<Sample>(client, "api/v1/thing", query, TimeSpan.FromMinutes(5), CancellationToken.None);
        var second = await cache.GetAsync<Sample>(client, "api/v1/thing", query, TimeSpan.FromMinutes(5), CancellationToken.None);

        first!.Value.Should().Be(42);
        second!.Value.Should().Be(42);
        handler.Requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task Cache_key_is_stable_across_query_order()
    {
        var handler = new FakeHttpMessageHandler();
        handler.RespondJson("api/v1/thing", """{ "value": 7 }""");

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var cache = TestCache.CreateInMemory();

        var a = new Dictionary<string, string?> { ["a"] = "1", ["b"] = "2" };
        var b = new Dictionary<string, string?> { ["b"] = "2", ["a"] = "1" };

        _ = await cache.GetAsync<Sample>(client, "api/v1/thing", a, TimeSpan.FromMinutes(5), CancellationToken.None);
        _ = await cache.GetAsync<Sample>(client, "api/v1/thing", b, TimeSpan.FromMinutes(5), CancellationToken.None);

        handler.Requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task Different_query_produces_a_second_request()
    {
        var handler = new FakeHttpMessageHandler();
        handler.RespondJson("api/v1/thing", """{ "value": 1 }""");

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var cache = TestCache.CreateInMemory();

        _ = await cache.GetAsync<Sample>(client, "api/v1/thing", new Dictionary<string, string?> { ["a"] = "1" }, TimeSpan.FromMinutes(5), CancellationToken.None);
        _ = await cache.GetAsync<Sample>(client, "api/v1/thing", new Dictionary<string, string?> { ["a"] = "2" }, TimeSpan.FromMinutes(5), CancellationToken.None);

        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Upstream_failure_propagates()
    {
        var handler = new FakeHttpMessageHandler();
        handler.RespondJson("api/v1/thing", "boom", HttpStatusCode.InternalServerError);

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var cache = TestCache.CreateInMemory();

        var act = async () => await cache.GetAsync<Sample>(client, "api/v1/thing", null, TimeSpan.FromMinutes(5), CancellationToken.None);
        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
