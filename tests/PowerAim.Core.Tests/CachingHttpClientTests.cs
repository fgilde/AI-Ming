using System.Net;
using PowerAim.Core;

namespace PowerAim.Core.Tests;

/// <summary>
///     The update check must see a release published a minute ago: it asks GitHub every time and only falls back
///     to the cached answer when the request fails. Store listings keep using the cache.
/// </summary>
public class CachingHttpClientTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "CachingHttpClientTests", Guid.NewGuid().ToString("N"));

    private sealed class Server : HttpMessageHandler
    {
        public string Body = "2.2.0.4";
        public bool Down;
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Down
                ? new HttpResponseMessage(HttpStatusCode.Forbidden) // GitHub's anonymous rate limit
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Body) });
        }
    }

    [Fact]
    public async Task A_fresh_request_sees_a_new_release_inside_the_cache_window()
    {
        var server = new Server();
        using var client = new CachingHttpClient(TimeSpan.FromHours(1), _dir, server);
        const string url = "https://api.github.com/repos/o/r/releases/latest";
        Assert.Equal("2.2.0.4", await client.GetAsync(url));

        server.Body = "3.0.0.1"; // released right after
        Assert.Equal("2.2.0.4", await client.GetAsync(url)); // listings: cached
        Assert.Equal("3.0.0.1", await client.GetAsync(url, preferFresh: true));

        server.Down = true;
        Assert.Equal("3.0.0.1", await client.GetAsync(url, preferFresh: true)); // falls back to the cache
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp */ }
    }
}
