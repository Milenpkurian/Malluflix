using Malluflix.Core.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Malluflix.Tests.TestSupport;

public static class TestCache
{
    public static ICachedHttpJson CreateInMemory() =>
        new CachedHttpJson(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            NullLogger<CachedHttpJson>.Instance);
}
