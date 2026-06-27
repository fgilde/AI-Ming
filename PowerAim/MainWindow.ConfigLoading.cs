using Core;
using Nextended.Core;
using Nextended.Core.Extensions;
using PowerAim.Config;
using PowerAim.InputLogic;
using PowerAim.Localizations;
using PowerAim.Types;
using PowerAim.UILibrary;
using PowerAim.Visuality;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace PowerAim;

public partial class MainWindow
{
    internal void LoadConfig(string path = AppConfig.DefaultConfigPath)
    {
        if (path == AppConfig.Current.Path)
            return;
        AppConfig.Current.Save();
        Console.WriteLine(Locale.LoadingConfigMessage + path);
        Config = AppConfig.Load(path);
        OnPropertyChanged(nameof(Config));

        if (!string.IsNullOrEmpty(AppConfig.Current.SuggestedModelName) && AppConfig.Current.SuggestedModelName != "N/A")
            MessageDialog.Show(
                $"{Locale.ModelSuggestionText}:\n" + AppConfig.Current.SuggestedModelName,
                Locale.SuggestedModel,
                MessageDialog.DialogButtons.OK,
                MessageDialog.DialogIcon.Info,
                owner: this);
        LoadModel();
    }

    private void LoadAntiRecoilConfig(string path = Constants.AntiRecoilConfigBasePath + "\\Default.cfg",
        bool loading_outside_startup = false)
    {
        if (!string.IsNullOrEmpty(path))
        {
            AppConfig.Current.AntiRecoilSettings.Load<AntiRecoilSettings>(path);
            if (loading_outside_startup)
                CreateUI();
        }
    }

    private void MenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        new ConfigSaver { Owner = this }.ShowDialog();
    }

    private void MenuItemSaveAs_OnClick(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog() { Filter = Locale.FilterConfig };
        if (dlg.ShowDialog() == true)
        {
            AppConfig.Current.Save(dlg.FileName);
        }
    }

    private void OpenConfig_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog() { Filter = Locale.FilterConfig };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            LoadConfig(dlg.FileName);
        }
    }

    private void AKeyChanger_ConfigOnGlobalKeyPressed(object? sender, EventArgs<(AKeyChanger Sender, string Key, StoredInputBinding KeyBinding)> e)
    {
        var args = e.Value;
        var configToLoad = args.Sender.Tag?.ToString();
        if (configToLoad is not null)
        {
            try
            {
                ConfigsListBox.SelectedIndex = ConfigsListBox.Items.IndexOf(configToLoad);
            }
            catch
            {
                Check.TryCatch<Exception>(() => LoadConfig(Path.Combine(Path.GetDirectoryName(AppConfig.DefaultConfigPath), configToLoad)));
            }
        }
    }

    private void DeleteConfig_Click(object sender, RoutedEventArgs e)
    {
        var cfg = (sender as FrameworkElement)?.Tag?.ToString();
        if (!string.IsNullOrEmpty(cfg))
            DeleteConfig(cfg);
    }

    private void DeleteConfig(string cfg, bool confirmed = false)
    {
        var path = Path.Combine(Path.GetDirectoryName(AppConfig.DefaultConfigPath), cfg);
        if (File.Exists(path))
        {
            if (!confirmed)
            {
                var res = MessageDialog.Show(
                    Locale.ConfirmConfigDelete.FormatWith(cfg), Locale.DeleteConfig,
                    MessageDialog.DialogButtons.YesNo,
                    MessageDialog.DialogIcon.Question,
                    owner: this,
                    defaultResult: MessageDialog.DialogResult.No);
                if (res == MessageDialog.DialogResult.No)
                    return;
            }
            File.Delete(path);
        }
    }
}
