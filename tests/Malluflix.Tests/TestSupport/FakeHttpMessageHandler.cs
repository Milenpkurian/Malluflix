using System.Net;
using System.Net.Http;
using System.Text;

namespace Malluflix.Tests.TestSupport;

/// <summary>Records outgoing requests and returns pre-canned responses keyed by absolute URL prefix.</summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    private readonly List<(Func<HttpRequestMessage, bool> Match, Func<HttpResponseMessage> Factory)> _responses = [];

    public FakeHttpMessageHandler RespondJson(string urlContains, string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        _responses.Add((
            req => req.RequestUri?.ToString().Contains(urlContains, StringComparison.Ordinal) is true,
            () => new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            }));
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        foreach (var (match, factory) in _responses)
        {
            if (match(request))
            {
                return Task.FromResult(factory());
            }
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        });
    }
}
