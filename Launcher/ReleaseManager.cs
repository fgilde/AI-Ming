using System.Collections.ObjectModel;
using System.Windows.Threading;
using Core;

public class ReleaseManager
{
    public static async Task LoadReleasesAsync(ObservableCollection<GitHubRelease> releases)
    {
        try
        {
            var manager = new GithubManager();
            var repoOwner = Constants.RepoOwner;
            var repoName = Constants.RepoName;

            var availableReleases = (await manager.GetAvailableReleasesAsync(repoOwner, repoName)).ToList();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                releases.Clear();
                foreach (var release in availableReleases)
                {
                    releases.Add(release);
                }
            });
        }
        catch (Exception e)
        {
            // Log error
        }
    }
}