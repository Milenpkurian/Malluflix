using System.Text.Json.Serialization;

namespace Malluflix.StremioAddon.Manifest;

public sealed record StremioManifest(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("logo")] string? Logo,
    [property: JsonPropertyName("background")] string? Background,
    [property: JsonPropertyName("resources")] IReadOnlyList<string> Resources,
    [property: JsonPropertyName("types")] IReadOnlyList<string> Types,
    [property: JsonPropertyName("idPrefixes")] IReadOnlyList<string> IdPrefixes,
    [property: JsonPropertyName("catalogs")] IReadOnlyList<StremioManifestCatalog> Catalogs);

public sealed record StremioManifestCatalog(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("extra")] IReadOnlyList<StremioManifestExtra>? Extra);

public sealed record StremioManifestExtra(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("isRequired")] bool? IsRequired = null);
