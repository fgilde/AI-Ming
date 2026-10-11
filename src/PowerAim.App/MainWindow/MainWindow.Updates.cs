using PowerAim.Core;
using PowerAim.Localization;
using PowerAim.UI.Visuality;
using System.Windows;
using Application = System.Windows.Application;

namespace PowerAim;

public partial class MainWindow
{
    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        await CheckUpdate(true);
    }

    private async Task CheckUpdate(bool showNotice)
    {
        try
        {
            CheckForUpdates.IsEnabled = false;
            UpdateCheckStatusLabel.Content = Locale.CheckingForUpdates;
            await Task.Delay(500);
            var updateManager = new UpdateManager();
            var hasUpdate = await updateManager.CheckForUpdate(ApplicationConstants.ApplicationVersion, ApplicationConstants.RepoOwner, ApplicationConstants.RepoName, ApplicationConstants.IsCudaBuild);
            UpdateCheckStatusLabel.Content = hasUpdate ? Locale.UpdateAvailable : Locale.NoUpdateAvailable;
            if (hasUpdate)
            {
                CheckForUpdates.Content = Locale.InstallUpdate;
            }
            if (!hasUpdate)
            {
                if (showNotice)
                    new NoticeBar(Locale.YouAreAlreadyOnTheLatestVersion, 5000).Show();
            }
            else
            {
                new UpdateDialog(updateManager) { Owner = Application.Current.MainWindow }.ShowDialog();
            }

            updateManager.Dispose();
        }
        finally
        {
            CheckForUpdates.IsEnabled = true;
        }
    }
}
