extern alias ApiHost;

using System.Net;
using System.Net.Http.Json;
using Malluflix.Core.Aggregation;
using Malluflix.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ApiProgram = ApiHost::Program;

namespace Malluflix.Tests.Integration;

[Collection("Integration")]
public class ApiIntegrationTests : IClassFixture<ApiIntegrationTests.Factory>
{
    private readonly Factory _factory;

    public ApiIntegrationTests(Factory factory) => _factory = factory;

    [Fact]
    public async Task Health_returns_ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Genres_returns_registered_ids()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/genres", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        payload.Should().Contain("Action");
    }

    [Fact]
    public async Task Search_requires_q_parameter()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/movies/search?q=", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Movie_by_invalid_imdb_id_returns_bad_request()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/movies/nope", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Theaters_returns_normalized_movies_from_stubbed_aggregator()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/movies/theaters", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<NormalizedMovie[]>();
        items.Should().NotBeNull();
        items!.Should().ContainSingle(m => m.ImdbId == "tt1234567");
    }

    public sealed class Factory : WebApplicationFactory<ApiProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Tmdb:ApiKey"] = "TESTKEY",
                });
            });

            builder.ConfigureServices(services =>
            {
                var aggregatorDescriptor = services.Single(d => d.ServiceType == typeof(IMalayalamMovieAggregator));
                services.Remove(aggregatorDescriptor);
                services.AddSingleton(_ => BuildAggregator());
            });
        }

        private static IMalayalamMovieAggregator BuildAggregator()
        {
            var mock = new Mock<IMalayalamMovieAggregator>();
            var sample = new NormalizedMovie(
                ImdbId: "tt1234567",
                TmdbId: 1,
                Title: "Sample",
                Overview: null,
                PosterUrl: null,
                BackdropUrl: null,
                ReleaseDate: null,
                RuntimeMinutes: null,
                Genres: [],
                Rating: null,
                IsInTheaters: true,
                IsOnOtt: false);
            mock.Setup(a => a.GetTheatersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<NormalizedMovie>)[sample]);
            mock.Setup(a => a.SearchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<NormalizedMovie>)[]);
            return mock.Object;
        }
    }
}
