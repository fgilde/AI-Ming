using System.Windows.Threading;
using PowerAim.Config;

namespace PowerAim.AILogic;

/// <summary>
///     Owns activation of <see cref="AimProfile"/> entries — the single source of truth for the
///     radio behaviour (one active at a time). Mirrors <see cref="AntiRecoilProfileManager"/>:
///     manual activation from the list row toggle / per-row <c>AKeyChanger</c> hotkey, plus an
///     OCR auto-switch poll. The crucial extra step versus AntiRecoil is that activating a profile
///     calls <see cref="AimProfile.Apply"/>, copying its values into the live global settings the
///     aim pipeline reads.
/// </summary>
public sealed class AimProfileManager : IDisposable
{
    private static AimProfileManager? _instance;
    public static AimProfileManager Instance => _instance ??= new AimProfileManager();

    private readonly DispatcherTimer _ocrTimer;
    private string? _lastOcrActivatedId;
    private bool _disposed;

    private AimProfileManager()
    {
        _ocrTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
        _ocrTimer.Tick += (_, _) => PollOcr();

        AppConfig.ConfigLoaded += OnConfigLoaded;
        if (AppConfig.Current != null) OnConfigLoaded(null, null!);
    }

    public static void EnsureInitialized() => _ = Instance;

    private void OnConfigLoaded(object? sender, EventArgs e)
    {
        if (!_ocrTimer.IsEnabled) _ocrTimer.Start();
        // Push the active profile's values into the live settings at startup so the pipeline
        // runs with the profile the user last had active.
        AppConfig.Current?.AimSettings?.ActiveProfile?.Apply();
    }

    // ========================================================================== OCR PATH ====

    private void PollOcr()
    {
        try
        {
            var settings = AppConfig.Current?.AimSettings;
            if (settings == null) return;
            if (AppConfig.Current?.OcrSettings?.Enabled != true) return;

            var ocr = OcrService.Instance.Latest;
            if (ocr.Count == 0) return;

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
                SetActiveProfile(p.Id, notify: _lastOcrActivatedId != p.Id);
                _lastOcrActivatedId = p.Id;
                return;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AimProfileManager] OCR poll failed: {ex.Message}");
        }
    }

    // ======================================================================= ACTIVATION ====

    /// <summary>
    ///     Set the radio-active profile and apply its values to the live settings. Empty string
    ///     clears the active id (leaves the live settings as-is). <paramref name="notify"/> shows a
    ///     transient on-screen notice (self-gated on the notifications toggle).
    /// </summary>
    public void SetActiveProfile(string id, bool notify)
    {
        var settings = AppConfig.Current?.AimSettings;
        if (settings == null) return;
        if (settings.ActiveProfileId == id) return;

        settings.ActiveProfileId = id ?? "";

        var profile = settings.ActiveProfile;
        profile?.Apply();

        if (!notify) return;
        if (string.IsNullOrEmpty(id))
            Notifier.Notify(Locale.AimProfileNone);
        else if (profile != null)
            Notifier.Notify(string.Format(Locale.AimProfileActiveFormat, profile.Name));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _ocrTimer.Stop();
        AppConfig.ConfigLoaded -= OnConfigLoaded;
    }
}
