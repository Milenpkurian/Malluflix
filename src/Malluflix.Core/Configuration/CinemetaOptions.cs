namespace Malluflix.Core.Configuration;

/// <summary>Bound from configuration section <c>Cinemeta</c>.</summary>
public sealed class CinemetaOptions
{
    public const string SectionName = "Cinemeta";

    public string BaseUrl { get; set; } = "https://v3-cinemeta.strem.io/";

    public int TimeoutSeconds { get; set; } = 10;
}
