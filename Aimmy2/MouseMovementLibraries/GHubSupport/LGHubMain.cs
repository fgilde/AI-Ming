using Other;
using System.Windows;

namespace Aimmy2.MouseMovementLibraries.GHubSupport
{
    internal class LGHubMain
    {
        public bool Load()
        {
            if (!RequirementsManager.CheckForGhub())
            {
                Aimmy2.Visuality.MessageDialog.Show("Unfortunately, LG HUB Mouse is not here.", "Aimmy", Aimmy2.Visuality.MessageDialog.DialogButtons.OK, Aimmy2.Visuality.MessageDialog.DialogIcon.Warning);
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
                    Aimmy2.Visuality.MessageDialog.Show("Unfortunately, LG HUB Mouse Movement mode cannot be ran sufficiently.\n" + ex.ToString(), "Aimmy", Aimmy2.Visuality.MessageDialog.DialogButtons.OK, Aimmy2.Visuality.MessageDialog.DialogIcon.Error);
                    return false;
                }
            }
            else
            {
                Aimmy2.Visuality.MessageDialog.Show("Memory Integrity is enabled. Please disable it to use LG HUB Mouse Movement mode.", "Aimmy", Aimmy2.Visuality.MessageDialog.DialogButtons.OK, Aimmy2.Visuality.MessageDialog.DialogIcon.Warning);
                return false;
            }
        }
    }
}