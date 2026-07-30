using System.Text.Json.Serialization;

namespace Malluflix.Core.Models.Tmdb;

public sealed record TmdbDiscoverResponse(
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("total_pages")] int TotalPages,
    [property: JsonPropertyName("total_results")] int TotalResults,
    [property: JsonPropertyName("results")] IReadOnlyList<TmdbMovie> Results);

public sealed record TmdbMovie(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("original_title")] string? OriginalTitle,
    [property: JsonPropertyName("original_language")] string? OriginalLanguage,
    [property: JsonPropertyName("overview")] string? Overview,
    [property: JsonPropertyName("poster_path")] string? PosterPath,
    [property: JsonPropertyName("backdrop_path")] string? BackdropPath,
    [property: JsonPropertyName("release_date")] string? ReleaseDate,
    [property: JsonPropertyName("vote_average")] double? VoteAverage,
    [property: JsonPropertyName("genre_ids")] IReadOnlyList<int>? GenreIds);

public sealed record TmdbExternalIds(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("imdb_id")] string? ImdbId);

public sealed record TmdbMovieDetails(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("imdb_id")] string? ImdbId,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("overview")] string? Overview,
    [property: JsonPropertyName("poster_path")] string? PosterPath,
    [property: JsonPropertyName("backdrop_path")] string? BackdropPath,
    [property: JsonPropertyName("release_date")] string? ReleaseDate,
    [property: JsonPropertyName("runtime")] int? Runtime,
    [property: JsonPropertyName("vote_average")] double? VoteAverage,
    [property: JsonPropertyName("genres")] IReadOnlyList<TmdbGenre>? Genres);

public sealed record TmdbGenre(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name);

public sealed record TmdbFindResponse(
    [property: JsonPropertyName("movie_results")] IReadOnlyList<TmdbMovie>? MovieResults);
