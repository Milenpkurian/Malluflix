using System.Text.Json.Serialization;

namespace Malluflix.Core.Models.Stremio;

public sealed record StremioCatalogResponse(
    [property: JsonPropertyName("metas")] IReadOnlyList<StremioCatalogItem> Metas);

public sealed record StremioMetaResponse(
    [property: JsonPropertyName("meta")] StremioMeta? Meta);

public sealed record StremioCatalogItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("poster")] string? Poster,
    [property: JsonPropertyName("posterShape")] string? PosterShape,
    [property: JsonPropertyName("background")] string? Background,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("releaseInfo")] string? ReleaseInfo,
    [property: JsonPropertyName("imdbRating")] string? ImdbRating,
    [property: JsonPropertyName("genres")] IReadOnlyList<string>? Genres);

public sealed record StremioMeta(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("poster")] string? Poster,
    [property: JsonPropertyName("background")] string? Background,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("releaseInfo")] string? ReleaseInfo,
    [property: JsonPropertyName("runtime")] string? Runtime,
    [property: JsonPropertyName("imdbRating")] string? ImdbRating,
    [property: JsonPropertyName("genres")] IReadOnlyList<string>? Genres);
