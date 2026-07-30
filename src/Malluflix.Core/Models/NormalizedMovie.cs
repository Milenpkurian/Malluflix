namespace Malluflix.Core.Models;

/// <summary>Host-agnostic, IMDb-keyed movie DTO produced by the aggregator.</summary>
public sealed record NormalizedMovie(
    string ImdbId,
    int TmdbId,
    string Title,
    string? Overview,
    string? PosterUrl,
    string? BackdropUrl,
    DateOnly? ReleaseDate,
    int? RuntimeMinutes,
    IReadOnlyList<string> Genres,
    double? Rating,
    bool IsInTheaters,
    bool IsOnOtt);
