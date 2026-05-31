using System.Collections.ObjectModel;
using Core;

public class ReleaseManager
{
    /// <summary>
    ///     Last error message from the most recent <see cref="LoadReleasesAsync"/> call.
    ///     <c>null</c> on success. Useful to surface to the user when the dropdown ends up empty.
    /// </summary>
    public static string? LastError { get; private set; }

    public static async Task LoadReleasesAsync(ObservableCollection<GitHubRelease> releases)
    {
        LastError = null;
        var sources = new (string Owner, string Name)[]
        {
            (Constants.RepoOwner, Constants.RepoName),
        };

        var manager = new GithubManager();
        try
        {
            List<GitHubRelease> all = new();
            Exception? lastException = null;

            foreach (var (owner, name) in sources)
            {
                try
                {
                    var fetched = (await manager.GetAvailableReleasesAsync(owner, name)).ToList();
                    if (fetched.Count > 0)
                    {
                        all = fetched;
                        break; // first source with releases wins (fork preferred)
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }
            }

            if (all.Count == 0 && lastException != null)
            {
                LastError = ShortError(lastException);
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                releases.Clear();
                foreach (var release in all) releases.Add(release);
            });
        }
        catch (Exception e)
        {
            LastError = ShortError(e);
        }
        finally
        {
            manager.Dispose();
        }
    }

    private static string ShortError(Exception e)
    {
        var msg = e.Message ?? string.Empty;
        // Note: the GithubManager now auto-falls back to the atom feed when the API is
        // rate-limited, so these messages only surface when BOTH the API and the (unlimited)
        // atom feed failed — typically a real network outage, not a rate limit. The wording
        // mentions the network rather than blaming the rate-limit to keep users from waiting
        // an hour for nothing.
        if (msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
            return "GitHub API and atom-feed fallback both failed. Check your internet connection.";
        return msg.Length > 160 ? msg.Substring(0, 160) + "…" : msg;
    }
}
