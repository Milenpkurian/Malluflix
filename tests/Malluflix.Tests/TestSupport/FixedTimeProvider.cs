namespace Malluflix.Tests.TestSupport;

public sealed class FixedTimeProvider(DateTime nowUtc) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(nowUtc, TimeSpan.Zero);
}
