using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using PowerAim.Config;

namespace PowerAim.AILogic;

/// <summary>
///     Drives the live aim feel from the aim PROFILES. There is no single "active" profile any more:
///     every enabled profile engages independently while its own aim-key is held (and its OCR
///     condition, if set, is met). Each frame the pipeline resolves the effective profile (see
///     <see cref="AimSettings.ResolveEffectiveProfile"/>) and calls <see cref="UpdateEffective"/>; on a
///     change this manager copies that profile's values into the live global settings the pipeline
///     reads — the globals are now purely internal plumbing, the UI only ever edits profiles. It also
///     runs a 750 ms OCR poll that refreshes each profile's <see cref="AimProfile.OcrConditionMet"/>
///     (an OCR weapon filter is now an extra GATE on engagement, not a profile switch).
/// </summary>
public sealed class AimProfileManager : IDisposable, INotifyPropertyChanged
{
    private static AimProfileManager? _instance;
    public static AimProfileManager Instance => _instance ??= new AimProfileManager();

    private readonly DispatcherTimer _ocrTimer;
    private bool _disposed;

    // The profile id whose values are currently copied into the live globals. Written only via
    // ApplyEffective (UI thread); read from the aim thread for change-detection → volatile.
    private volatile string _appliedEffectiveId = "\0"; // sentinel so the first resolve always applies
    private string _effectiveProfileId = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     Raised whenever the effective (currently-driving) profile changes. Static so each
    ///     <see cref="AimProfile"/>'s <c>IsEffective</c> can react without instantiating the manager
    ///     (and forcing its <see cref="DispatcherTimer"/> onto the wrong thread) during deserialization.
    /// </summary>
    public static event Action? EffectiveProfileChanged;

    /// <summary>
    ///     Id of the profile currently DRIVING the aim (its aim-key is held + OCR met), or "" when none.
    ///     Surfaced so the UI can badge whichever profile is live. Observable.
    /// </summary>
    public string EffectiveProfileId
    {
        get => _effectiveProfileId;
        private set
        {
            if (_effectiveProfileId == value) return;
            _effectiveProfileId = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EffectiveProfileId)));
        }
    }

    private AimProfileManager()
    {
        // Bind the OCR timer explicitly to the UI dispatcher so it ticks even if the singleton happens
        // to be first touched from the aim/thread-pool thread (otherwise the timer would never fire and
        // OcrConditionMet would freeze). The 4-arg ctor auto-starts; OnConfigLoaded (re)starts as needed.
        var disp = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _ocrTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(750), DispatcherPriority.Normal,
            (_, _) => PollOcr(), disp);
        _ocrTimer.Stop();

        AppConfig.ConfigLoaded += OnConfigLoaded;
        if (AppConfig.Current != null) OnConfigLoaded(null, null!);
    }

    public static void EnsureInitialized() => _ = Instance;

    private void OnConfigLoaded(object? sender, EventArgs e)
    {
        if (!_ocrTimer.IsEnabled) _ocrTimer.Start();
        // A reloaded config has different profiles/ids — force the next resolve to re-apply cleanly
        // instead of matching a stale applied-id.
        _appliedEffectiveId = "\0";
        PollOcr(); // seed OcrConditionMet so a freshly-loaded config gates correctly from frame one
    }

    // ========================================================================== OCR PATH ====

    /// <summary>
    ///     Refresh each profile's <see cref="AimProfile.OcrConditionMet"/>. No filter set → always met.
    ///     OCR globally off → treat as met (a configured filter can't be evaluated, so it must not
    ///     silently block aiming). Runs on the UI-thread timer; the per-frame resolver just reads the flag.
    /// </summary>
    private void PollOcr()
    {
        try
        {
            var settings = AppConfig.Current?.AimSettings;
            if (settings == null) return;
            bool ocrOn = AppConfig.Current?.OcrSettings?.Enabled == true;
            var ocr = ocrOn ? OcrService.Instance.Latest : null;

            foreach (var p in settings.Profiles.ToArray())
            {
                if (!ocrOn || !p.AutoSwitchOnOcr
                    || string.IsNullOrWhiteSpace(p.OcrRegionName) || string.IsNullOrWhiteSpace(p.WeaponMatch))
                {
                    p.OcrConditionMet = true;
                    continue;
                }

                bool met = false;
                if (ocr != null && ocr.TryGetValue(p.OcrRegionName, out var reading) && reading.Confidence >= 0.3f)
                {
                    var haystack = (reading.Text ?? reading.Raw ?? "").Trim();
                    met = haystack.Length > 0 && haystack.Contains(p.WeaponMatch, StringComparison.OrdinalIgnoreCase);
                }
                p.OcrConditionMet = met;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AimProfileManager] OCR poll failed: {ex.Message}");
        }
    }

    // ================================================================= EFFECTIVE PROFILE ====

    /// <summary>
    ///     Called every frame by the aim pipeline with the resolved effective profile (null = nothing
    ///     engaged). Copies its feel into the live globals ONLY when the effective profile changes
    ///     (cheap; the pipeline keeps reading globals). Marshals to the UI thread because
    ///     <see cref="AimProfile.Apply"/> writes config that is data-bound to UI controls.
    /// </summary>
    public void UpdateEffective(AimProfile? profile)
    {
        string id = profile?.Id ?? "";
        if (id == _appliedEffectiveId) return; // unchanged → nothing to do

        var disp = Application.Current?.Dispatcher;
        // No dispatcher (app teardown / headless) → can't safely run Apply (it writes UI-bound config).
        if (disp == null) return;
        if (disp.CheckAccess())
            ApplyEffective(profile, id);
        else
            disp.BeginInvoke(new Action(() => ApplyEffective(profile, id)));
    }

    /// <summary>
    ///     Re-push a profile's values to the live globals if it is the one currently driving the aim,
    ///     so editing the live profile in the editor takes effect immediately (the per-frame resolver
    ///     only re-applies on an id CHANGE, not on value edits). UI-thread only.
    /// </summary>
    public void ReapplyIfEffective(AimProfile? profile)
    {
        if (profile != null && profile.Id == _appliedEffectiveId) profile.Apply();
    }

    private void ApplyEffective(AimProfile? profile, string id)
    {
        _appliedEffectiveId = id;
        profile?.Apply(); // null (nothing engaged) → leave the last globals; the pipeline won't aim anyway
        EffectiveProfileId = id;
        EffectiveProfileChanged?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _ocrTimer.Stop();
        AppConfig.ConfigLoaded -= OnConfigLoaded;
    }
}
