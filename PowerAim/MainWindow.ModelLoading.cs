using Core;
using Microsoft.Xaml.Behaviors.Core;
using Nextended.Core;
using Nextended.Core.Extensions;
using Nextended.UI.Helper;
using PowerAim.Class;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim.InputLogic;
using PowerAim.Localizations;
using PowerAim.Other;
using PowerAim.Types;
using PowerAim.UILibrary;
using PowerAim.Visuality;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PowerAim;

public partial class MainWindow
{
    internal void FillMenus()
    {
        UpdateModelText();
        ModelContextMenu.Items.Clear();
        ModelContextMenu.Items.AddRange(ModelListBox.ToMenuItems(item =>
        {
            LoadModel(item.Header.ToString());
        },
        (i, item) => i <= 9 ? KeyGestureConvertHelper.CreateFromString($"Ctrl + Shift + {i}") : null));
        ModelContextMenu.Items.Add(new Separator());
        var downloadableMenu = new System.Windows.Controls.MenuItem()
        {
            Header = Locale.DownloadableModelsHeader
        };
        ModelContextMenu.Items.Add(downloadableMenu);
        downloadableMenu.Items.AddRange(_availableModels.Keys.Select(s => new System.Windows.Controls.MenuItem()
        {
            Header = s,
            Command = new ActionCommand(() =>
            {
                downloadableMenu.IsEnabled = false;
                ADownloadGateway.DownloadAsync(s, "models").ContinueWith(task =>
                {
                    if (task.Result)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            LoadModel(s);
                            FillMenus();
                        });
                    }
                });
            })
        }));

        MenuItemOpenCfg.Items.Clear();
        MenuItemOpenCfg.Items.AddRange(ConfigsListBox.ToMenuItems(
            item => LoadConfig(Path.Combine(Path.GetDirectoryName(AppConfig.DefaultConfigPath), item.Header?.ToString())),
            (i, item) => i <= 9 ? KeyGestureConvertHelper.CreateFromString($"Ctrl + {i}") : null)
        );
    }

    private void UpdateModelText()
    {
        if (Config is not null)
            ModelContextMenu.Header = $"{Config.LastLoadedModel} ({AIManager?.PredictionLogic?.ExecutionProvider})";
    }

    private void LoadModel(string? modelName = null)
    {
        FileManager.AIManager?.Dispose();
        FileManager.AIManager = null;
        FileManager.CurrentlyLoadingModel = false;
        LoadLastModel(modelName);
    }

    private void LoadLastModel(string? modelName = null)
    {
        modelName ??= Config.LastLoadedModel;
        var lastLoaded = Path.Combine(ApplicationConstants.ModelsBasePath, modelName);
        var modelPath = File.Exists(lastLoaded) ? lastLoaded : Path.Combine(ApplicationConstants.ModelsBasePath, ApplicationConstants.DefaultModel);
        if (File.Exists(modelPath) && !FileManager.CurrentlyLoadingModel &&
            FileManager.AIManager?.IsModelLoaded != true)
        {
            // A model is about to load — keep the empty-state card in its "loading" look so the
            // "no model" message doesn't flash during the brief load.
            ModelLoadPending = true;
            _ = _fileManager.LoadModel(Path.GetFileName(modelPath), modelPath);
        }
        else if (!IsModelLoaded)
        {
            // Nothing to load (no model file present) — reveal the empty-state message now.
            ModelLoadPending = false;
        }
        UpdateModelText();
    }

    /// <summary>
    ///     Copies the bundled default model (<c>Resources\default.onnx</c>, shipped next to the exe)
    ///     into <c>bin\models\</c> if it isn't there yet, then loads it. Backs the "Load default
    ///     model" button on <see cref="UILibrary.NoModelCard"/>. Throws a localized
    ///     <see cref="FileNotFoundException"/> if the bundled file is missing so the card can show it.
    /// </summary>
    public async Task LoadDefaultModelAsync()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var modelsDir = Path.Combine(baseDir, "bin", "models");
        Directory.CreateDirectory(modelsDir);
        var dest = Path.Combine(modelsDir, ApplicationConstants.DefaultModel);

        if (!File.Exists(dest))
        {
            var src = Path.Combine(baseDir, "Resources", ApplicationConstants.DefaultModel);
            if (!File.Exists(src))
                throw new FileNotFoundException(Locale.LoadDefaultModelMissing);
            File.Copy(src, dest);
        }

        // Copy the FP16 sibling next to it too so Precision = Auto/FP16 can pick it up (see
        // PredictionLogic.ResolveModelPath). Done independently of the FP32 copy above so upgrades —
        // where bin\models\default.onnx already existed before the FP16 model shipped — get it as well.
        const string fp16Name = "default.fp16.onnx";
        var fp16Dest = Path.Combine(modelsDir, fp16Name);
        if (!File.Exists(fp16Dest))
        {
            var fp16Src = Path.Combine(baseDir, "Resources", fp16Name);
            if (File.Exists(fp16Src))
                try { File.Copy(fp16Src, fp16Dest); } catch { /* fp16 optional — fall back to FP32 */ }
        }

        // Switch the empty-state card to its loading look while the model spins up.
        ModelLoadPending = true;
        await _fileManager.LoadModel(ApplicationConstants.DefaultModel,
            Path.Combine(ApplicationConstants.ModelsBasePath, ApplicationConstants.DefaultModel));
    }

    private void AKeyChanger_ModelOnGlobalKeyPressed(object? sender, EventArgs<(AKeyChanger Sender, string Key, StoredInputBinding KeyBinding)> e)
    {
        var args = e.Value;
        var modelToLoad = args.Sender.Tag?.ToString();
        if (modelToLoad is not null)
        {
            try
            {
                ModelListBox.SelectedIndex = ModelListBox.Items.IndexOf(modelToLoad);
            }
            catch
            {
                Check.TryCatch<Exception>(() => LoadModel(modelToLoad));
            }
        }
    }

    private void DeleteModel_Click(object sender, RoutedEventArgs e)
    {
        var model = (sender as FrameworkElement)?.Tag?.ToString();
        if (!string.IsNullOrEmpty(model))
            DeleteModel(model);
    }

    private void DeleteModel(string model, bool confirmed = false)
    {
        var path = Path.Combine(Constants.ModelsBasePath, model);
        if (File.Exists(path))
        {
            if (!confirmed)
            {
                var res = MessageDialog.Show(
                    Locale.ConfirmModelDelete.FormatWith(model), Locale.DeleteModel,
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
