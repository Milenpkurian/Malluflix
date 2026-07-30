namespace Malluflix.Core.Configuration;

/// <summary>Bound from configuration section <c>Tmdb</c>. Required in production.</summary>
public sealed class TmdbOptions
{
    public const string SectionName = "Tmdb";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.themoviedb.org/3";

    public string ImageBaseUrl { get; set; } = "https://image.tmdb.org/t/p";

    public string PosterSize { get; set; } = "w500";

    public string BackdropSize { get; set; } = "w1280";

    public string Region { get; set; } = "IN";

    public string OriginalLanguage { get; set; } = "ml";

    public int RecentReleaseWindowDays { get; set; } = 90;

    public int UpstreamPageSize { get; set; } = 20;
}
