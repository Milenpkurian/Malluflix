using System.Reflection;
using Malluflix.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Malluflix.StremioAddon.Manifest;

public interface IManifestBuilder
{
    StremioManifest Build();
}

public sealed class ManifestBuilder(IOptionsMonitor<CatalogOptions> options) : IManifestBuilder
{
    private const string AddonId = "community.malluflix.dotnet";
    private const string AddonName = "MalluFlix (.NET)";
    private const string AddonDescription =
        "Latest Malayalam movies in theaters and on OTT, plus genre catalogs. Metadata only — no hosting, no scraping.";

    public StremioManifest Build()
    {
        var catalogs = options.CurrentValue.BuildCatalogs()
            .Select(c => new StremioManifestCatalog(
                Id: c.Id,
                Type: "movie",
                Name: c.Name,
                Extra: BuildExtras(c)))
            .ToArray();

        return new StremioManifest(
            Id: AddonId,
            Version: DetectVersion(),
            Name: AddonName,
            Description: AddonDescription,
            Logo: null,
            Background: null,
            Resources: ["catalog", "meta"],
            Types: ["movie"],
            IdPrefixes: ["tt"],
            Catalogs: catalogs);
    }

    private static StremioManifestExtra[] BuildExtras(CatalogDescriptor descriptor) =>
        descriptor.SupportsSearch
            ? [new("skip"), new("search")]
            : [new("skip")];

    private static string DetectVersion() =>
        typeof(ManifestBuilder).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(ManifestBuilder).Assembly.GetName().Version?.ToString(3)
            ?? "1.0.0";
}
