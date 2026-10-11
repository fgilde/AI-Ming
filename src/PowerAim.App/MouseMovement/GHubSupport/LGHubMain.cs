using PowerAim.Other;
using System.Windows;

namespace PowerAim.MouseMovement.GHubSupport;

internal class LGHubMain
{
    public bool Load()
    {
        if (!RequirementsManager.CheckForGhub())
        {
            PowerAim.UI.Visuality.MessageDialog.Show("Unfortunately, LG HUB Mouse is not here.", "PowerAim", PowerAim.UI.Visuality.MessageDialog.DialogButtons.OK, PowerAim.UI.Visuality.MessageDialog.DialogIcon.Warning);
            return false;
        }

        if (RequirementsManager.IsMemoryIntegrityEnabled())
        {
            try
            {
                LGMouse.Open();
                LGMouse.Close();
                return true;
            }
            catch (Exception ex)
            {
                PowerAim.UI.Visuality.MessageDialog.Show("Unfortunately, LG HUB Mouse Movement mode cannot be ran sufficiently.\n" + ex.ToString(), "PowerAim", PowerAim.UI.Visuality.MessageDialog.DialogButtons.OK, PowerAim.UI.Visuality.MessageDialog.DialogIcon.Error);
                return false;
            }
        }
        else
        {
            PowerAim.UI.Visuality.MessageDialog.Show("Memory Integrity is enabled. Please disable it to use LG HUB Mouse Movement mode.", "PowerAim", PowerAim.UI.Visuality.MessageDialog.DialogButtons.OK, PowerAim.UI.Visuality.MessageDialog.DialogIcon.Warning);
            return false;
        }
    }
}