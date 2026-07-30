# MalluFlix (.NET)

A .NET 10 solution that surfaces recent **Malayalam movie** releases — in theaters and on streaming — as:

1. A REST API (`Malluflix.Api`) with OpenAPI docs, rate limiting, and Serilog logging.
2. A [Stremio](https://www.stremio.com/) addon (`Malluflix.StremioAddon`) that publishes the same catalog under an install-in-one-click manifest.

Both apps share a `Malluflix.Core` library that aggregates The Movie Database (TMDB) via a resilient, cached HTTP pipeline and bridges TMDB ids to IMDb ids so results plug straight into Stremio and other TT-id ecosystems.

> **Important.** MalluFlix is a **metadata catalog** only. It does not host, index, or link to any video streams. If you install the Stremio addon, you will need a separate playback source (a legitimate streaming service or another addon you already trust) to actually watch anything. See [Legal & content policy](#legal--content-policy).

---

## Repository layout

```
Malluflix.slnx
Directory.Build.props        # net10.0, nullable, TreatWarningsAsErrors, analyzers
Directory.Packages.props     # Central Package Management
global.json                  # pinned SDK
src/
  Malluflix.Core/            # options, models, providers, cache, aggregator
  Malluflix.Api/             # Minimal API host
  Malluflix.StremioAddon/    # Stremio addon host + landing page
tests/
  Malluflix.Tests/           # xUnit + FluentAssertions + Moq
docker/
  api.Dockerfile
  addon.Dockerfile
.github/workflows/
  ci.yml                     # build + test + coverage
  docker.yml                 # multi-image push to GHCR
```

---

## Quick start

### 1. Prerequisites

- .NET 10 SDK (`10.0.203` — pinned via `global.json`).
- A free TMDB **API v3 key** — https://developer.themoviedb.org/reference/intro/authentication.

### 2. Configure

The TMDB key is required at startup. Prefer environment variables so it never lands in source control:

```powershell
$env:Tmdb__ApiKey = "your-tmdb-v3-key"
```

Optional overrides (all have sensible defaults, see `appsettings.json`):

| Setting                                                                      | Default      | Notes                                        |
| ---------------------------------------------------------------------------- | ------------ | -------------------------------------------- |
| `Tmdb__Region`                                                               | `IN`         | Region gate for release-date discovery.      |
| `Tmdb__OriginalLanguage`                                                     | `ml`         | ISO 639-1 for Malayalam.                     |
| `Tmdb__RecentReleaseWindowDays`                                              | `90`         | Rolling window for "in theaters" / "on OTT". |
| `Catalogs__EnableTheaters` / `EnableOtt` / `EnableUpcoming` / `EnableGenres` | `true`       | Toggle catalog groups.                       |
| `Catalogs__IdPrefix`                                                         | `malayalam_` | Stremio catalog id prefix.                   |

### 3. Run locally

```powershell
# REST API — http://localhost:5000
dotnet run --project src/Malluflix.Api

# Stremio addon — http://localhost:5001
dotnet run --project src/Malluflix.StremioAddon
```

Open the addon URL in a browser to see the landing page. Click **Install in Stremio** and it will launch Stremio with the local manifest URL.

---

## REST API endpoints

Base: `/api/movies` (rate-limited: 60 req/min per IP, `429` on overflow).

| Method | Path                                     | Description                                            |
| ------ | ---------------------------------------- | ------------------------------------------------------ |
| GET    | `/api/movies/theaters?skip=0`            | Malayalam movies currently in theaters (last 90 days). |
| GET    | `/api/movies/ott?skip=0`                 | Malayalam movies newly available on OTT/digital.       |
| GET    | `/api/movies/upcoming?skip=0`            | Upcoming Malayalam movies.                             |
| GET    | `/api/movies/genre/{tmdbGenreId}?skip=0` | Genre listing (only registered genres accepted).       |
| GET    | `/api/movies/search?q=...&skip=0`        | Full-text search across TMDB Malayalam titles.         |
| GET    | `/api/movies/{imdbId}`                   | Single normalized movie by IMDb id (`ttXXXXXXX`).      |
| GET    | `/api/genres`                            | Registered genre id → name map.                        |
| GET    | `/health`                                | Liveness probe.                                        |
| GET    | `/openapi/v1.json`                       | OpenAPI 3.1 document (Development only).               |

Skip-based pagination; page size is 20.

---

## Stremio addon endpoints

Base: root. Follows the [Stremio Addon Protocol](https://github.com/Stremio/stremio-addon-sdk/blob/master/docs/api/README.md).

| Method | Path                                             | Description                                                                    |
| ------ | ------------------------------------------------ | ------------------------------------------------------------------------------ |
| GET    | `/manifest.json`                                 | Addon manifest — resources: `catalog`, `meta`; type: `movie`; id prefix: `tt`. |
| GET    | `/catalog/movie/{catalogId}.json`                | First page of a catalog.                                                       |
| GET    | `/catalog/movie/{catalogId}/skip={n}.json`       | Paginated catalog page.                                                        |
| GET    | `/catalog/movie/{catalogId}/search={query}.json` | Search inside a search-capable catalog.                                        |
| GET    | `/meta/movie/{imdbId}.json`                      | Movie metadata (Cinemeta preferred, TMDB fallback).                            |
| GET    | `/health`                                        | Liveness probe.                                                                |
| GET    | `/`                                              | Landing page with install button.                                              |

**Install URLs** (Stremio):

- Desktop app: `stremio://<host>/manifest.json`
- Stremio Web: `https://web.stremio.com/#/addons?addon=<url-encoded https://<host>/manifest.json>`
- Or paste the plain `https://<host>/manifest.json` into the addon search bar.

---

## Docker

Multi-stage images (SDK-10 → aspnet-10, non-root, healthcheck):

```powershell
docker build -f docker/api.Dockerfile   -t malluflix-api   .
docker build -f docker/addon.Dockerfile -t malluflix-addon .

docker run --rm -p 8080:8080 -e Tmdb__ApiKey=your-key malluflix-api
docker run --rm -p 8081:8080 -e Tmdb__ApiKey=your-key malluflix-addon
```

Prebuilt images are published to GHCR on every push to `main` via [`.github/workflows/docker.yml`](.github/workflows/docker.yml).

### Deploy to Azure

Both images are single-process ASP.NET workloads that listen on `$PORT` (via `ASPNETCORE_URLS=http://+:8080`), so they slot into:

- **Azure App Service for Containers** — set `Tmdb__ApiKey` under Configuration → Application settings.
- **Azure Container Apps** — set `Tmdb__ApiKey` as a secret and reference it as an env var; scale to 0 is fine (catalogs are cached).

---

## Configuration reference

Keys are read via `IConfiguration` (`appsettings.json`, env vars with `__` separator, user secrets, etc.).

| Key                                                              | Purpose                                                 |
| ---------------------------------------------------------------- | ------------------------------------------------------- |
| `Tmdb__ApiKey`                                                   | **Required.** TMDB v3 key.                              |
| `Tmdb__BaseUrl`                                                  | TMDB REST base (`https://api.themoviedb.org/3`).        |
| `Tmdb__ImageBaseUrl` / `Tmdb__PosterSize` / `Tmdb__BackdropSize` | CDN base + image sizes.                                 |
| `Tmdb__Region` / `Tmdb__OriginalLanguage`                        | Discovery gates.                                        |
| `Tmdb__RecentReleaseWindowDays`                                  | Rolling "recent" window.                                |
| `Tmdb__UpstreamPageSize`                                         | TMDB page size (fixed at 20 upstream).                  |
| `Catalogs__IdPrefix`                                             | Stremio catalog id prefix (default `malayalam_`).       |
| `Catalogs__Enable{Theaters,Ott,Upcoming,Genres}`                 | Toggle catalog groups.                                  |
| `Catalogs__Genres`                                               | Optional override map `{ tmdbGenreId: displayName }`.   |
| `Serilog__*`                                                     | Standard Serilog configuration section.                 |
| `ASPNETCORE_URLS`                                                | Listening URL (containers set this to `http://+:8080`). |

---

## Development

```powershell
# restore + build + tests + analyzer clean-build
dotnet build -warnaserror
dotnet test
```

- **Central Package Management** — all versions live in `Directory.Packages.props`; project files carry `<PackageReference>` without a version.
- **Strict analyzers** — `TreatWarningsAsErrors=true` + `AnalysisMode=Recommended`. CA1848 / CA1515 / CA2007 are pre-suppressed in `.editorconfig`.
- **HTTP resilience** — every outbound call goes through `AddStandardResilienceHandler` (retry with jitter, circuit breaker, 15 s attempt / 45 s total timeouts).
- **Caching** — `IDistributedCache` (in-memory today, swap to Redis with `AddStackExchangeRedisCache` without touching call sites). TTLs: catalog 24 h, meta 1 h, search 5 min.
- **Concurrency** — TMDB → IMDb bridge lookups are gated at 5 concurrent requests via `SemaphoreSlim`.

---

## Legal & content policy

- **Metadata only.** MalluFlix does not host, mirror, index, or link to any copyrighted audio, video, or subtitle streams. All catalog, image, and metadata content is fetched live from TMDB and Cinemeta under their respective terms.
- **Attribution.** Movie data © [The Movie Database (TMDB)](https://www.themoviedb.org/). This project is not endorsed or certified by TMDB. Metadata proxied via [Cinemeta](https://github.com/Stremio/stremio-addons) is © the Stremio project.
- **TMDB terms.** By running this software you agree to TMDB's [API terms of use](https://www.themoviedb.org/api-terms-of-use). Do not redistribute a shared TMDB key.
- **Regional & language filtering.** Filtering to Malayalam (`original_language=ml`) and India (`region=IN`) is a discovery preference, not a claim of ownership over that catalog.
- **No warranty.** Provided as-is under the [MIT License](LICENSE) (add one before publishing). Users are responsible for complying with the terms of any playback addon or streaming service they combine this with.

If you are a rights holder and believe content is being surfaced incorrectly, please file an issue and it will be addressed promptly.
