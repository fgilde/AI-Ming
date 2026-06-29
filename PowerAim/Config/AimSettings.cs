using System.Collections.ObjectModel;
using PowerAim.InputLogic;

namespace PowerAim.Config;

/// <summary>
///     Holds the named <see cref="AimProfile"/> list. There is no single-active radio: every
///     <see cref="AimProfile.Enabled"/> profile is live and drives the aim while its own aim-key is held
///     (resolved per-frame by <see cref="ResolveEffectiveProfile"/>); the effective profile's values are
///     pushed into the live globals by <see cref="AILogic.AimProfileManager"/> when it changes.
/// </summary>
public class AimSettings : BaseSettings
{
    public ObservableCollection<AimProfile> Profiles
    {
        get;
        set => SetField(ref field, value);
    } = new();

    /// <summary>
    ///     No longer a runtime selector (profiles engage by their own held key). Kept only as a
    ///     migration / reconcile anchor: the profile the legacy globals were migrated into, and the
    ///     target <see cref="ReconcileActiveProfileId"/> keeps pointing at a real profile.
    /// </summary>
    public string ActiveProfileId
    {
        get;
        set => SetField(ref field, value ?? "");
    } = "";

    /// <summary>Migration flag. 0 = pre-profiles; 1 = default seeded; 2 = per-profile keys/EMA/prediction.</summary>
    public int SchemaVersion
    {
        get;
        set => SetField(ref field, value);
    }

    /// <summary>Returns the active profile, or <c>null</c> when none is active.</summary>
    public AimProfile? ActiveProfile
    {
        get
        {
            if (string.IsNullOrEmpty(ActiveProfileId)) return null;
            foreach (var p in Profiles)
                if (p.Id == ActiveProfileId) return p;
            return null;
        }
    }

    /// <summary>
    ///     Seed a single "Default" profile from the current live global aim settings the first time
    ///     this config loads under the profile schema, so existing setups keep their feel and the
    ///     list is never empty. Idempotent once <see cref="SchemaVersion"/> reaches 1. Always runs
    ///     <see cref="ReconcileActiveProfileId"/> so a dangling/empty active id is repaired.
    /// </summary>
    public void MigrateLegacyIfNeeded()
    {
        if (SchemaVersion >= 2)
        {
            ReconcileActiveProfileId();
            return;
        }

        // v0 → no profiles yet: seed a Default from the live globals (CaptureFromGlobals also grabs the
        // legacy EMA/prediction settings now).
        if (Profiles.Count == 0)
        {
            var seeded = new AimProfile { Name = "Default" };
            seeded.CaptureFromGlobals();
            // Inherit the old global aim-disengage rules so existing pause behaviour keeps working
            // (they're now per-profile). Only the Default profile gets them; new profiles start empty.
            var globalRules = AppConfig.Current?.AimDisengageRules;
            if (globalRules != null)
                foreach (var r in globalRules) seeded.DisengageRules.Add(r);
            Profiles.Add(seeded);
            ActiveProfileId = seeded.Id;
        }

        // v0 + v1 → the aim KEY, EMA and PREDICTION settings used to be GLOBAL. Move them into the
        // profile that was live (the previously-active one) so it keeps working unchanged now that the
        // global aim key is gone; other profiles keep their own captured feel but need a key assigned
        // in the editor. Runs once (SchemaVersion → 2).
        var target = ActiveProfile ?? (Profiles.Count > 0 ? Profiles[0] : null);
        if (target != null)
        {
            var cfg = AppConfig.Current;
            if (!target.HasAimKey && cfg?.BindingSettings?.AimKeyBindings != null)
                foreach (var k in cfg.BindingSettings.AimKeyBindings)
                    if (k is { IsValid: true } && !target.AimKeyBindings.Any(e => e.Equals(k)))
                        target.AimKeyBindings.Add(k);
            if (cfg?.ToggleState != null)
            {
                target.EmaSmoothing = cfg.ToggleState.EMASmoothening;
                target.Predictions = cfg.ToggleState.Predictions;
            }
            if (cfg?.SliderSettings != null) target.EmaSmoothingFactor = cfg.SliderSettings.EMASmoothening;
            if (cfg?.DropdownState != null) target.PredictionMethod = cfg.DropdownState.PredictionMethod;
        }

        SchemaVersion = 2;
        ReconcileActiveProfileId();
    }

    /// <summary>
    ///     Resolve which profile DRIVES the aim right now. Every <see cref="AimProfile.Enabled"/> profile
    ///     whose OCR condition is met (<see cref="AimProfile.OcrConditionMet"/>) and one of whose own
    ///     <see cref="AimProfile.AimKeyBindings"/> is currently held is a candidate; the one whose held
    ///     key is most specific (most components, so a single key can't shadow a chord) wins, with list
    ///     order as the tiebreak. Returns <c>null</c> when nothing is engaged (→ not aiming). There is no
    ///     global-key fallback any more — a profile only aims while its OWN key is held.
    /// </summary>
    public AimProfile? ResolveEffectiveProfile()
    {
        try
        {
            AimProfile? best = null;
            int bestSpecificity = 0;
            // Snapshot both levels: the UI thread may add/remove profiles OR edit a profile's aim-keys
            // (via MultiKeyChanger) while the aim thread enumerates here.
            foreach (var p in Profiles.ToArray())
            {
                if (!p.Enabled || !p.OcrConditionMet) continue;
                var keys = p.AimKeyBindings;
                if (keys == null) continue;
                int held = 0;
                foreach (var k in keys.ToArray())
                {
                    if (k is not { IsValid: true }) continue;
                    if (!InputBindingManager.IsHoldingBindingFor(k, TimeSpan.FromSeconds(k.MinTime))) continue;
                    int spec = k.IsCombo ? k.Components!.Count : 1;
                    if (spec > held) held = spec;
                }
                // First profile with the highest specificity wins (strict >), so list order is the tiebreak.
                if (held > bestSpecificity) { bestSpecificity = held; best = p; }
            }
            return best;
        }
        catch
        {
            // A profile / aim-key collection was mutated on the UI thread mid-resolve. Never crash the
            // aim loop — nothing engaged this frame; next frame resolves cleanly.
            return null;
        }
    }

    /// <summary>Snap <see cref="ActiveProfileId"/> to a real profile when it's empty or dangling.</summary>
    private void ReconcileActiveProfileId()
    {
        if (Profiles.Count == 0)
        {
            if (!string.IsNullOrEmpty(ActiveProfileId)) ActiveProfileId = "";
            return;
        }
        if (!string.IsNullOrEmpty(ActiveProfileId))
        {
            foreach (var p in Profiles)
                if (p.Id == ActiveProfileId) return;
        }
        AimProfile? pick = null;
        foreach (var p in Profiles) { if (p.IsValid) { pick = p; break; } }
        ActiveProfileId = (pick ?? Profiles[0]).Id;
    }
}
