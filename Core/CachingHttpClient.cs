using System.Net.Http.Headers;
using Nextended.Core.Encode;

namespace Core;

public class CachingHttpClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;
    private readonly TimeSpan _cacheDuration;

    public HttpRequestHeaders DefaultRequestHeaders => _httpClient.DefaultRequestHeaders;

    public CachingHttpClient(TimeSpan? cacheDuration = null, string cacheDirectory = "")
    {
        if (string.IsNullOrEmpty(cacheDirectory))
            cacheDirectory = Path.Combine(Path.GetTempPath(), "CachingHttpClient");
        
        cacheDuration ??= TimeSpan.FromMinutes(10);

        _httpClient = new HttpClient();
        _cacheDirectory = cacheDirectory;
        _cacheDuration = cacheDuration.Value;

        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
    }

    public async Task<string> GetAsync(string url)
    {
        string cacheFile = GetCacheFilePath(url);

        if (File.Exists(cacheFile) && DateTime.Now - File.GetCreationTime(cacheFile) < _cacheDuration)
        {
            return await File.ReadAllTextAsync(cacheFile);
        }

        if (File.Exists(cacheFile))
            File.Delete(cacheFile);

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        string content = await response.Content.ReadAsStringAsync();

        _= File.WriteAllTextAsync(cacheFile, content);

        return content;
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