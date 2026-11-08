using System.Net.Http.Headers;
using Nextended.Core.Encode;

namespace PowerAim.Core;

public class CachingHttpClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;
    private readonly TimeSpan _cacheDuration;

    public HttpRequestHeaders DefaultRequestHeaders => _httpClient.DefaultRequestHeaders;

    public CachingHttpClient(TimeSpan? cacheDuration = null, string cacheDirectory = "", HttpMessageHandler? handler = null)
    {
        if (string.IsNullOrEmpty(cacheDirectory))
            cacheDirectory = Path.Combine(Path.GetTempPath(), "CachingHttpClient");
        
        cacheDuration ??= TimeSpan.FromMinutes(10);

        _httpClient = handler == null ? new HttpClient() : new HttpClient(handler);
        _cacheDirectory = cacheDirectory;
        _cacheDuration = cacheDuration.Value;

        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
    }

    /// <param name="preferFresh">
    ///     Ask the server even inside the cache window and use the cache only when that fails. For answers that
    ///     must not lag behind, like the update check: a cached "latest release" hid a new release for an hour.
    /// </param>
    public async Task<string> GetAsync(string url, bool preferFresh = false)
    {
        string cacheFile = GetCacheFilePath(url);
        bool haveCache = File.Exists(cacheFile);

        // Fresh cache → serve it without touching the network.
        if (!preferFresh && haveCache && DateTime.Now - File.GetLastWriteTime(cacheFile) < _cacheDuration)
        {
            return await File.ReadAllTextAsync(cacheFile);
        }

        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            string content = await response.Content.ReadAsStringAsync();
            // Overwrite (refreshes LastWriteTime). Awaited so the cache is durable before return.
            await File.WriteAllTextAsync(cacheFile, content);
            return content;
        }
        catch
        {
            // Network failure or GitHub rate-limit (403/429): fall back to the stale cache if we
            // have one, so the UI keeps showing the last-known-good data instead of breaking or
            // re-hammering the API. The stale entry is intentionally NOT deleted up front (the old
            // code deleted it before the request, destroying the only fallback on a failed call).
            if (haveCache) return await File.ReadAllTextAsync(cacheFile);
            throw;
        }
    }

    static string GetHash(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        return text.EncodeDecode().Base64.Encode();
    }

    private string GetCacheFilePath(string url)
    {
        string fileName = GetHash(url);
        return Path.Combine(_cacheDirectory, fileName);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}