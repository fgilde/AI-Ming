using System.Text.Json;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Core
{
    public class GithubManager : IDisposable
    {
        private readonly CachingHttpClient httpClient;

        public GithubManager()
        {
            httpClient = new CachingHttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Aimmy2");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        }


        public class Asset
        {
            public string Name { get; set; }
            public string DownloadUrl { get; set; }
        }

        private class GitHubContent
        {
            public string name { get; set; }
        }

        public async Task<IEnumerable<GitHubRelease>> GetAvailableReleasesAsync(string owner, string repo)
        {
            string apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases";

            var content = await httpClient.GetAsync(apiUrl);
            var data = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(content);

            return data.Select(d => new GitHubRelease
            {
                TagName = d["tag_name"].ToString(),
                Description = d["body"].ToString(),
                Assets = ((JsonElement)d["assets"]).EnumerateArray().Select(a => new Asset
                {
                    Name = a.GetProperty("name").ToString(),
                    DownloadUrl = a.GetProperty("browser_download_url").ToString()
                }).ToList()
            });
        }

        public async Task<GitHubRelease> GetLatestReleaseInfoAsync(string owner, string repo)
        {
            string apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

            var content = await httpClient.GetAsync(apiUrl);
            
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

            string tagName = data["tag_name"].ToString() ?? throw new InvalidOperationException("Tag name is missing in the response");
            string description = data["body"].ToString();
            var assets = ((JsonElement)data["assets"]).EnumerateArray().Select(a => new Asset
            {
                Name = a.GetProperty("name").ToString(),
                DownloadUrl = a.GetProperty("browser_download_url").ToString()
            }).ToList();

            return new GitHubRelease
            {
                TagName = tagName,
                Description = description,
                Assets = assets
            };
        }

        public async Task<IEnumerable<string>> FetchGithubFilesAsync(string url)
        {
            var content = await httpClient.GetAsync(url);
            
            List<GitHubContent>? contents = JsonConvert.DeserializeObject<List<GitHubContent>>(content);
            if (contents == null)
            {
                throw new InvalidOperationException("Failed to deserialize GitHub content or Github content is empty.");
            }

            return contents.Select(c => c.name);
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }
    }
}
