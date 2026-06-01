using System.Windows.Threading;
using PowerAim.Config;

namespace PowerAim.AILogic;

/// <summary>
///     Owns the activation logic of <see cref="AntiRecoilProfile"/> entries.
///     <list type="bullet">
///       <item><b>OCR auto-switch</b>: a low-rate <see cref="DispatcherTimer"/> reads the configured
///             OCR region's text and switches to the first profile whose <c>WeaponMatch</c>
///             substring is present. Profile activation triggers a transient on-screen notice via
///             <see cref="Notifier"/> (when AntiRecoil master toggle is on and global notifications
///             are enabled).</item>
///       <item><b>Manual activation</b>: <see cref="SetActiveProfile"/> is called from the list-row
///             toggle, the per-row <c>AKeyChanger</c> callback, and the OCR poll. It's the single
///             source of truth for the radio behaviour: setting one profile active automatically
///             clears whatever was active before.</item>
///     </list>
///     The keybind registration itself is owned by the per-row <c>AKeyChanger</c> control — it
///     persists the binding under <c>BindingSettings</c> using the
///     <c>ANTIRECOIL_PROFILE_&lt;id&gt;</c> key, so the manager doesn't need to subscribe directly.
/// </summary>
public sealed class AntiRecoilProfileManager : IDisposable
{
    private static AntiRecoilProfileManager? _instance;
    public static AntiRecoilProfileManager Instance => _instance ??= new AntiRecoilProfileManager();

    private readonly DispatcherTimer _ocrTimer;
    private string? _lastOcrActivatedId;
    private bool _disposed;

    private AntiRecoilProfileManager()
    {
        _ocrTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
        _ocrTimer.Tick += (_, _) => PollOcr();

        AppConfig.ConfigLoaded += OnConfigLoaded;
        if (AppConfig.Current != null) OnConfigLoaded(null, null!);
    }

    public static void EnsureInitialized()
    {
        _ = Instance;
    }

    private void OnConfigLoaded(object? sender, EventArgs e)
    {
        if (!_ocrTimer.IsEnabled) _ocrTimer.Start();
    }

    // ========================================================================== OCR PATH ====

    private void PollOcr()
    {
        try
        {
            var settings = AppConfig.Current?.AntiRecoilSettings;
            if (settings == null) return;
            if (AppConfig.Current?.OcrSettings?.Enabled != true) return;
            if (AppConfig.Current?.ToggleState?.AntiRecoil != true) return;

            var ocr = OcrService.Instance.Latest;
            if (ocr.Count == 0) return;

            // Find the first profile with AutoSwitchOnOcr=true whose configured OCR region's text
            // contains the WeaponMatch substring. First-wins keeps it predictable — users can
            // reorder profiles to bias which wins on ambiguous reads.
            foreach (var p in settings.Profiles)
            {
                if (!p.AutoSwitchOnOcr) continue;
                if (string.IsNullOrWhiteSpace(p.OcrRegionName) || string.IsNullOrWhiteSpace(p.WeaponMatch)) continue;
                if (!ocr.TryGetValue(p.OcrRegionName, out var reading)) continue;
                if (reading.Confidence < 0.3f) continue;

                var haystack = (reading.Text ?? reading.Raw ?? "").Trim();
                if (haystack.Length == 0) continue;
                if (!haystack.Contains(p.WeaponMatch, StringComparison.OrdinalIgnoreCase)) continue;

                if (settings.ActiveProfileId == p.Id) { _lastOcrActivatedId = p.Id; return; }
                // Suppress double-notifications when OCR flickers in/out around the confidence
                // threshold but keeps matching the same profile.
                SetActiveProfile(p.Id, notify: _lastOcrActivatedId != p.Id);
                _lastOcrActivatedId = p.Id;
                return;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AntiRecoilProfileManager] OCR poll failed: {ex.Message}");
        }
    }

    // ======================================================================= ACTIVATION ====

    /// <summary>
    ///     Set the radio-active profile. Empty string clears it. When <paramref name="notify"/> is
    ///     true and the master AntiRecoil toggle is on, the user sees a transient on-screen notice
    ///     (gated by <see cref="ToggleState.ShowToggleNotifications"/> inside <see cref="Notifier"/>).
    /// </summary>
    public void SetActiveProfile(string id, bool notify)
    {
        var settings = AppConfig.Current?.AntiRecoilSettings;
        if (settings == null) return;
        if (settings.ActiveProfileId == id) return;

        settings.ActiveProfileId = id ?? "";

        if (!notify) return;
        // Notify regardless of whether the master AntiRecoil toggle is on — this lets the user
        // pre-configure a profile (e.g. while the game is loading) and see confirmation that the
        // hotkey landed. Notifier.Notify still self-gates on ShowToggleNotifications.

        if (string.IsNullOrEmpty(id))
        {
            Notifier.Notify(Locale.AntiRecoilProfileNone);
        }
        else
        {
            var profile = settings.ActiveProfile;
            if (profile != null)
                Notifier.Notify(string.Format(Locale.AntiRecoilProfileActiveFormat, profile.Name));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _ocrTimer.Stop();
        AppConfig.ConfigLoaded -= OnConfigLoaded;
    }
}
