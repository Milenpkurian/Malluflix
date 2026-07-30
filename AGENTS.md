# AGENTS.md — Malluflix (.NET)

Metadata-only Malayalam-movie catalog. Two ASP.NET Core hosts share one core library that aggregates TMDB + Cinemeta and bridges TMDB ↔ IMDb ids.

See [README.md](README.md) for user-facing docs, config keys, deploy steps, and the legal policy — do **not** duplicate that content here.

## Architecture at a glance

- [Malluflix.Core](src/Malluflix.Core) — options, models, cached HTTP providers, TMDB↔IMDb bridge, `MalayalamMovieAggregator`. Exposes one composition root: `AddMalluflixCore(configuration)` in [CoreServiceCollectionExtensions.cs](src/Malluflix.Core/DependencyInjection/CoreServiceCollectionExtensions.cs).
- [Malluflix.Api](src/Malluflix.Api) — Minimal API host. Endpoints in [MoviesEndpoints.cs](src/Malluflix.Api/Endpoints/MoviesEndpoints.cs), rate-limited under policy `"api"` (60/min).
- [Malluflix.StremioAddon](src/Malluflix.StremioAddon) — Stremio addon host. Endpoints in [StremioEndpoints.cs](src/Malluflix.StremioAddon/Endpoints/StremioEndpoints.cs), manifest built by [ManifestBuilder.cs](src/Malluflix.StremioAddon/Manifest/ManifestBuilder.cs), landing page in [wwwroot/](src/Malluflix.StremioAddon/wwwroot).
- [Malluflix.Tests](tests/Malluflix.Tests) — xUnit + FluentAssertions + Moq. Reuse fakes in [TestSupport/](tests/Malluflix.Tests/TestSupport): `FakeHttpMessageHandler`, `FakeTimeProvider`, `TestCache`. Do not introduce new HTTP mocking libraries.

Data flow: `Endpoint → IMalayalamMovieAggregator → ITmdbClient / ICinemetaClient (typed HttpClient + resilience + cache) → ITmdbImdbBridge → NormalizedMovie`.

## Build & test

```powershell
dotnet build -warnaserror   # matches CI; TreatWarningsAsErrors is on
dotnet test
dotnet run --project src/Malluflix.Api          # http://localhost:5000
dotnet run --project src/Malluflix.StremioAddon # http://localhost:5001
```

The SDK is pinned to `10.0.203` via [global.json](global.json). The solution file is the new `.slnx` format ([Malluflix.slnx](Malluflix.slnx)) — most tools accept it, but if a command rejects it, target the project path directly.

## Conventions that will break the build if ignored

- **Central Package Management.** Add packages via `<PackageReference Include="X" />` (no `Version`) in the csproj, and add the version once in [Directory.Packages.props](Directory.Packages.props).
- **Analyzers as errors.** [Directory.Build.props](Directory.Build.props) sets `TreatWarningsAsErrors=true` and `AnalysisMode=Recommended`. Only CA1848, CA1515, CA2007 are pre-suppressed in [.editorconfig](.editorconfig) — do **not** add `ConfigureAwait(false)` or `LoggerMessage` source generators just to silence analyzers.
- **File-scoped namespaces** (`csharp_style_namespace_declarations = file_scoped:warning`).
- **Nullable + implicit usings enabled** globally.
- Keep `public sealed partial class Program;` at the bottom of each host's `Program.cs` — it exists so `WebApplicationFactory<Program>` tests can bind.

## Domain rules — hard constraints

- **Metadata only.** Do not add code that scrapes, hosts, indexes, or links to video/audio/subtitle streams, or anything that resembles playback resolution. See the legal section of [README.md](README.md#legal--content-policy). Reject requests that would violate this and explain why.
- **No shared TMDB key in code or fixtures.** Configuration comes from `Tmdb__ApiKey` (env var, user secrets, or `appsettings.*.json` that stays uncommitted). Tests use `"TESTKEY"` against `FakeHttpMessageHandler`.

## Patterns to follow when extending

- **Outbound HTTP** always goes through a typed `HttpClient` registered in `AddMalluflixCore` with `.AddStandardResilienceHandler(ConfigureResilience)`. Do not `new HttpClient()` and do not bypass the resilience pipeline.
- **Caching** goes through `ICachedHttpJson` (backed by `IDistributedCache`, currently in-memory — swappable to Redis without changing call sites). Follow the TTL tiers already in use: catalog 24 h, meta 1 h, search 5 min.
- **Options** are bound with `.Bind(...).Validate(...).ValidateOnStart()`. New sections should follow the same shape and expose a `public const string SectionName`.
- **Time** is injected via `TimeProvider` (registered as `TimeProvider.System`, overridden in tests with `FakeTimeProvider`). Never call `DateTime.UtcNow` directly in `Malluflix.Core`.
- **Concurrency.** The TMDB→IMDb bridge is gated with a `SemaphoreSlim(5)` — preserve this bound if you extend the bridge.
- **Logging.** Serilog is configured in each host's `Program.cs` with the bootstrap-logger pattern and `try/catch (not HostAbortedException)/finally { CloseAndFlush }`. Preserve this shape.
- **Endpoints.** Both hosts use Minimal API `MapGroup` + extension methods (`MapMovies`, `MapStremio`). Add new endpoints inside these groups rather than at the root.
- **Stremio ids.** The addon manifest declares `idPrefixes: ["tt"]`. All catalog/meta ids surfaced to Stremio must be IMDb ids — bridge from TMDB via `ITmdbImdbBridge`.

## Test conventions

- Test naming: `Method_scenario_expected` (snake-cased scenario), e.g. `GetTheatersAsync_calls_discover_with_release_type_3`.
- Build the SUT with a small local `Build(...)` helper per fixture (see [MalayalamMovieAggregatorTests.cs](tests/Malluflix.Tests/Aggregation/MalayalamMovieAggregatorTests.cs)) — no shared setup base classes.
- Assert with FluentAssertions (`.Should().Contain(...)`), not raw `Assert.*`.
- Inspect outbound HTTP by reading `FakeHttpMessageHandler.Requests` and asserting on query strings — that's the established pattern for verifying TMDB call construction.

## CI, Docker, deploy

- CI: [.github/workflows/ci.yml](.github/workflows/ci.yml) runs `dotnet build --no-restore -warnaserror` then `dotnet test` with coverage. Keep it green.
- Docker images: [docker/api.Dockerfile](docker/api.Dockerfile), [docker/addon.Dockerfile](docker/addon.Dockerfile). Both listen on `http://+:8080` via `ASPNETCORE_URLS`. Preserve non-root + healthcheck if you edit them.
- Multi-image publish: [.github/workflows/docker.yml](.github/workflows/docker.yml) → GHCR on push to `main`.
