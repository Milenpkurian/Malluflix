using Microsoft.Extensions.Options;

namespace Malluflix.Tests.TestSupport;

public static class StaticOptionsMonitor
{
    public static IOptionsMonitor<T> For<T>(T value) where T : class => new Impl<T>(value);

    private sealed class Impl<T>(T value) : IOptionsMonitor<T> where T : class
    {
        public T CurrentValue { get; } = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
