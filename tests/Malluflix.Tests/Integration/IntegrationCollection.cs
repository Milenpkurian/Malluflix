namespace Malluflix.Tests.Integration;

// The two host Programs share Serilog's static Log.Logger; keep integration tests in a single non-parallel collection.
[CollectionDefinition("Integration", DisableParallelization = true)]
#pragma warning disable CA1711 // Collection suffix is required by xUnit.
public sealed class IntegrationCollection;
#pragma warning restore CA1711
