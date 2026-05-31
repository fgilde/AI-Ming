using Other;
using System.Windows;

namespace PowerAim.MouseMovementLibraries.GHubSupport;

internal class LGHubMain
{
    public bool Load()
    {
        if (!RequirementsManager.CheckForGhub())
        {
            PowerAim.Visuality.MessageDialog.Show("Unfortunately, LG HUB Mouse is not here.", "PowerAim", PowerAim.Visuality.MessageDialog.DialogButtons.OK, PowerAim.Visuality.MessageDialog.DialogIcon.Warning);
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
                PowerAim.Visuality.MessageDialog.Show("Unfortunately, LG HUB Mouse Movement mode cannot be ran sufficiently.\n" + ex.ToString(), "PowerAim", PowerAim.Visuality.MessageDialog.DialogButtons.OK, PowerAim.Visuality.MessageDialog.DialogIcon.Error);
                return false;
            }
        }
        else
        {
            PowerAim.Visuality.MessageDialog.Show("Memory Integrity is enabled. Please disable it to use LG HUB Mouse Movement mode.", "PowerAim", PowerAim.Visuality.MessageDialog.DialogButtons.OK, PowerAim.Visuality.MessageDialog.DialogIcon.Warning);
            return false;
        }
    }
}