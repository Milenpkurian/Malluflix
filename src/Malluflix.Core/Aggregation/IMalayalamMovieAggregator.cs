using Malluflix.Core.Models;
using Malluflix.Core.Models.Stremio;

namespace Malluflix.Core.Aggregation;

public interface IMalayalamMovieAggregator
{
    Task<IReadOnlyList<NormalizedMovie>> GetTheatersAsync(int skip, CancellationToken cancellationToken);

    Task<IReadOnlyList<NormalizedMovie>> GetOttAsync(int skip, CancellationToken cancellationToken);

    Task<IReadOnlyList<NormalizedMovie>> GetUpcomingAsync(int skip, CancellationToken cancellationToken);

    Task<IReadOnlyList<NormalizedMovie>> GetByGenreAsync(int tmdbGenreId, int skip, CancellationToken cancellationToken);

    Task<IReadOnlyList<NormalizedMovie>> SearchAsync(string query, CancellationToken cancellationToken);

    Task<StremioMeta?> GetMetaAsync(string imdbId, CancellationToken cancellationToken);

    Task<NormalizedMovie?> GetNormalizedAsync(string imdbId, CancellationToken cancellationToken);
}
