using PowerAim.Core;
using Microsoft.Xaml.Behaviors.Core;
using Nextended.Core;
using Nextended.Core.Extensions;
using Nextended.UI.Helper;
using PowerAim.Class;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim.Input;
using PowerAim.Localization;
using PowerAim.Other;
using PowerAim.Types;
using PowerAim.UI.UILibrary;
using PowerAim.UI.Visuality;
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
        // The status-strip header is bound to ModelStatusText; just nudge it to re-read (model + the
        // actually-active execution provider). Setting Header directly here fought the binding before.
        CallPropertyChanged(nameof(ModelStatusText));
    }

    private void LoadModel(string? modelName = null)
    {
        // Do NOT dispose here. FileManager.LoadModel owns the atomic swap:
        // create new manager first, then dispose the previous one off the UI thread.
        // Pre-disposing raced the AI loop against a still-running InferenceSession.Run
        // and crashed the process on model download / switch.
        if (FileManager.CurrentlyLoadingModel)
            return;
        LoadLastModel(modelName);
    }

    private void LoadLastModel(string? modelName = null)
    {
        modelName ??= Config.LastLoadedModel;
        // Resolve against the app base directory — relative paths fail when cwd differs.
        string modelsDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ApplicationConstants.ModelsBasePath));
        Directory.CreateDirectory(modelsDir);
        var lastLoaded = Path.Combine(modelsDir, modelName ?? string.Empty);
        var modelPath = File.Exists(lastLoaded)
            ? lastLoaded
            : Path.Combine(modelsDir, ApplicationConstants.DefaultModel);
        // Always attempt load when a file exists and no load is already in flight.
        // FileManager.LoadModel performs the safe create-then-dispose swap; do not gate on
        // AIManager?.IsModelLoaded (that blocked legitimate model switches).
        if (File.Exists(modelPath) && !FileManager.CurrentlyLoadingModel)
        {
            ModelLoadPending = true;
            if (_fileManager != null)
                _ = _fileManager.LoadModel(Path.GetFileName(modelPath), modelPath);
            else
                ModelLoadPending = false;
        }
        else if (!IsModelLoaded)
        {
            ModelLoadPending = false;
        }
        UpdateModelText();
    }

    /// <summary>
    ///     Copies the bundled default model (<c>Resources\default.onnx</c>, shipped next to the exe)
    ///     into <c>bin\models\</c> if it isn't there yet, then loads it. Backs the "Load default
    ///     model" button on <see cref="PowerAim.UI.UILibrary.NoModelCard"/>. Throws a localized
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
