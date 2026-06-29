using System.Text.Json;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Core
{
    /// <summary>
    /// Metadata for a file discovered on a GitHub repo. Carries everything required
    /// to download the file directly (<see cref="DownloadUrl"/>) plus the originating
    /// repo coordinates so callers can disambiguate when merging across repos.
    /// </summary>
    public record GitHubFile(string Name, string DownloadUrl, string Sha, string Owner, string Repo, string Path);

    public class GithubManager : IDisposable
    {
        private readonly CachingHttpClient httpClient;

        public GithubManager()
        {
            // 1-hour disk cache: model/config listings and release info change rarely, so this keeps
            // repeated launches well under GitHub's 60-req/hour anonymous limit. On a rate-limit or
            // network error CachingHttpClient additionally serves the stale cache, so the store
            // never breaks just because the window was exhausted.
            httpClient = new CachingHttpClient(TimeSpan.FromHours(1));
            httpClient.DefaultRequestHeaders.Add("User-Agent", "PowerAim");
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
            public string download_url { get; set; }
            public string sha { get; set; }
        }

        public async Task<IEnumerable<GitHubRelease>> GetAvailableReleasesAsync(string owner, string repo)
        {
            string apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases";

            try
            {
                var content = await httpClient.GetAsync(apiUrl);
                var data = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(content);
                if (data != null && data.Count > 0)
                {
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
            }
            catch (HttpRequestException ex) when (
                ex.Message.Contains("403", StringComparison.Ordinal) ||
                ex.Message.Contains("429", StringComparison.Ordinal) ||
                ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
            {
                // Anonymous GitHub API access is limited to 60 requests/hour/IP. The atom feed
                // below is NOT rate-limited (it's a regular HTML/XML endpoint), so we fall back to
                // it. Asset URLs are reconstructed from build.ps1's naming convention since the
                // atom feed doesn't carry them.
            }
            catch (Exception)
            {
                // Any other failure — DNS, certificate, transient 5xx, JSON parse — also flips us
                // to the atom-feed fallback rather than burning the call.
            }

            return await GetReleasesViaAtomAsync(owner, repo);
        }

        /// <summary>
        ///     Rate-limit-free release list via the public atom feed
        ///     (<c>https://github.com/{owner}/{repo}/releases.atom</c>). Returns the same
        ///     <see cref="GitHubRelease"/> shape as the API path. Asset URLs are constructed from
        ///     PowerAim's build.ps1 naming convention — if a release was published with non-standard
        ///     zip names, those assets won't be discoverable through this path, but the dropdown
        ///     still lists the tag so the user can navigate manually.
        /// </summary>
        public async Task<IEnumerable<GitHubRelease>> GetReleasesViaAtomAsync(string owner, string repo)
        {
            string atomUrl = $"https://github.com/{owner}/{repo}/releases.atom";
            string xml;
            try { xml = await httpClient.GetAsync(atomUrl); }
            catch { return []; }

            var doc = System.Xml.Linq.XDocument.Parse(xml);
            System.Xml.Linq.XNamespace atom = "http://www.w3.org/2005/Atom";
            var entries = doc.Descendants(atom + "entry").Select(e =>
            {
                var title = e.Element(atom + "title")?.Value ?? "";
                var content = e.Element(atom + "content")?.Value ?? "";
                return new GitHubRelease
                {
                    TagName = title.Trim(),
                    Description = StripHtml(content),
                    Assets = BuildKnownAssetsForTag(owner, repo, title.Trim()),
                };
            }).ToList();
            return entries;
        }

        /// <summary>
        ///     Constructs asset URLs that PowerAim's <c>build.ps1</c> is known to produce for any
        ///     given tag:
        ///     <list type="bullet">
        ///       <item><c>Release_{tag}.zip</c> — DirectML build</item>
        ///       <item><c>Release_{tag}_cuda.zip</c> — CUDA build</item>
        ///       <item><c>Installer.exe</c> — standalone installer</item>
        ///     </list>
        ///     The URLs are well-formed even if a specific asset doesn't exist at that path —
        ///     GitHub returns 404 then and the launcher's download step surfaces it normally.
        /// </summary>
        private static List<Asset> BuildKnownAssetsForTag(string owner, string repo, string tag)
        {
            string baseUrl = $"https://github.com/{owner}/{repo}/releases/download/{tag}";
            return
            [
                new Asset { Name = $"Release_{tag}.zip",      DownloadUrl = $"{baseUrl}/Release_{tag}.zip" },
                new Asset { Name = $"Release_{tag}_cuda.zip", DownloadUrl = $"{baseUrl}/Release_{tag}_cuda.zip" },
                new Asset { Name = "Installer.exe",            DownloadUrl = $"{baseUrl}/Installer.exe" },
            ];
        }

        private static string StripHtml(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            // Cheap tag-strip — atom feeds wrap content in <p>/<ul>/<li>. Don't pull in HtmlAgility
            // for two regexes.
            s = System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", " ");
            s = System.Net.WebUtility.HtmlDecode(s);
            return System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();
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

        /// <summary>
        /// Lists the contents of <paramref name="path"/> in the given repo and returns rich
        /// <see cref="GitHubFile"/> records, including the direct <c>download_url</c> from the
        /// GitHub API response. Use this when you need to download files later without
        /// reconstructing URLs from constants.
        /// </summary>
        public async Task<IEnumerable<GitHubFile>> FetchGithubFilesDetailedAsync(string owner, string repo, string path)
        {
            string apiUrl = $"https://api.github.com/repos/{owner}/{repo}/contents/{path}";
            var content = await httpClient.GetAsync(apiUrl);

            List<GitHubContent>? contents = JsonConvert.DeserializeObject<List<GitHubContent>>(content);
            if (contents == null)
            {
                throw new InvalidOperationException("Failed to deserialize GitHub content or Github content is empty.");
            }

            return contents
                .Where(c => !string.IsNullOrEmpty(c.name))
                .Select(c => new GitHubFile(
                    Name: c.name,
                    DownloadUrl: c.download_url ?? $"https://github.com/{owner}/{repo}/raw/main/{path}/{c.name}",
                    Sha: c.sha ?? string.Empty,
                    Owner: owner,
                    Repo: repo,
                    Path: $"{path}/{c.name}"));
        }

        /// <summary>
        /// Returns the timestamp of the latest commit that touched <paramref name="path"/> in
        /// <paramref name="owner"/>/<paramref name="repo"/>. Used by the cross-repo merger to
        /// resolve filename conflicts ("newer wins"). Returns <c>null</c> on any error or when
        /// the commit list is empty so the caller can fall back to its tie-break rule.
        /// </summary>
        public async Task<DateTime?> GetLatestCommitDateAsync(string owner, string repo, string path)
        {
            try
            {
                string apiUrl = $"https://api.github.com/repos/{owner}/{repo}/commits?path={Uri.EscapeDataString(path)}&per_page=1";
                var content = await httpClient.GetAsync(apiUrl);

                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                    return null;

                var commit = doc.RootElement[0];
                if (!commit.TryGetProperty("commit", out var commitInner)) return null;

                // Prefer committer.date, fall back to author.date.
                JsonElement dateElement = default;
                if (commitInner.TryGetProperty("committer", out var committer) &&
                    committer.TryGetProperty("date", out var committerDate))
                {
                    dateElement = committerDate;
                }
                else if (commitInner.TryGetProperty("author", out var author) &&
                         author.TryGetProperty("date", out var authorDate))
                {
                    dateElement = authorDate;
                }

                if (dateElement.ValueKind != JsonValueKind.String) return null;

                if (DateTime.TryParse(dateElement.GetString(), null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
                {
                    return parsed.ToUniversalTime();
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }
    }
}
