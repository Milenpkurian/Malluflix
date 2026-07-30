extern alias AddonHost;

using System.Net;
using System.Net.Http.Json;
using Malluflix.Core.Aggregation;
using Malluflix.Core.Models;
using Malluflix.Core.Models.Stremio;
using Malluflix.StremioAddon.Manifest;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using AddonProgram = AddonHost::Program;

namespace Malluflix.Tests.Integration;

[Collection("Integration")]
public class StremioIntegrationTests : IClassFixture<StremioIntegrationTests.Factory>
{
    private readonly Factory _factory;

    public StremioIntegrationTests(Factory factory) => _factory = factory;

    [Fact]
    public async Task Manifest_is_valid_stremio_shape()
    {
        var client = _factory.CreateClient();

        var manifest = await client.GetFromJsonAsync<StremioManifest>(new Uri("/manifest.json", UriKind.Relative));

        manifest.Should().NotBeNull();
        manifest!.Resources.Should().BeEquivalentTo(["catalog", "meta"]);
        manifest.Types.Should().BeEquivalentTo(["movie"]);
        manifest.IdPrefixes.Should().BeEquivalentTo(["tt"]);
        manifest.Catalogs.Should().Contain(c => c.Id == "malayalam_theaters");
    }

    [Fact]
    public async Task Catalog_returns_metas_from_stubbed_aggregator()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/catalog/movie/malayalam_theaters.json", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<StremioCatalogResponse>();
        body.Should().NotBeNull();
        body!.Metas.Should().ContainSingle(m => m.Id == "tt1234567");
    }

    [Fact]
    public async Task Meta_with_invalid_id_returns_null_meta()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/meta/movie/not-an-id.json", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<StremioMetaResponse>();
        body!.Meta.Should().BeNull();
    }

    public sealed class Factory : WebApplicationFactory<AddonProgram>
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
            return mock.Object;
        }
    }
}
