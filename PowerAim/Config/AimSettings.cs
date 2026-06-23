using System.Collections.ObjectModel;

namespace PowerAim.Config;

/// <summary>
///     Holds the named <see cref="AimProfile"/> list and which one is active (radio). Mirrors
///     <see cref="AntiRecoilSettings"/>. The active profile's values are pushed into the live global
///     settings via <see cref="AimProfile.Apply"/> whenever it changes — see
///     <see cref="AILogic.AimProfileManager"/>.
/// </summary>
public class AimSettings : BaseSettings
{
    public ObservableCollection<AimProfile> Profiles
    {
        get;
        set => SetField(ref field, value);
    } = new();

    /// <summary>Id of the active profile. Empty = none active (no profile drives the aim settings).</summary>
    public string ActiveProfileId
    {
        get;
        set => SetField(ref field, value ?? "");
    } = "";

    /// <summary>Migration flag. 0 = pre-profiles; 1 = a default profile has been seeded.</summary>
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
        if (SchemaVersion >= 1)
        {
            ReconcileActiveProfileId();
            return;
        }
        if (Profiles.Count > 0)
        {
            SchemaVersion = 1;
            ReconcileActiveProfileId();
            return;
        }

        var seeded = new AimProfile { Name = "Default" };
        seeded.CaptureFromGlobals();
        // Inherit the old global aim-disengage rules so existing pause behaviour keeps working
        // (they're now per-profile). Only the Default profile gets them; new profiles start empty.
        var globalRules = AppConfig.Current?.AimDisengageRules;
        if (globalRules != null)
            foreach (var r in globalRules) seeded.DisengageRules.Add(r);
        Profiles.Add(seeded);
        ActiveProfileId = seeded.Id;
        SchemaVersion = 1;
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
