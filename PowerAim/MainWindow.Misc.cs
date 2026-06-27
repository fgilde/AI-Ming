using PowerAim.Visuality;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Button = System.Windows.Controls.Button;

namespace PowerAim;

public partial class MainWindow
{
    private void OpenFolderB_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button clickedButton)
            Process.Start("explorer.exe", Path.Combine(Directory.GetCurrentDirectory(), "bin", clickedButton.Tag.ToString()));
    }

    private double CalculateAngleDifference(double targetAngle, double fullCircle, double halfCircle, double clamp)
    {
        var angleDifference = (targetAngle - currentGradientAngle + fullCircle) % fullCircle;
        if (angleDifference > halfCircle) angleDifference -= fullCircle;
        return Math.Max(Math.Min(angleDifference, clamp), -clamp);
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        OutputTextBox.Clear();
    }

    private void ShowKnownIssues_Click(object sender, RoutedEventArgs e)
    {
        KnownIssuesDialog.ShowIf(this, true);
    }

    private void UIElement_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void ToggleMagnifier(bool? show = null)
    {
        if (_magnifier is null && show is null or true)
        {
            _magnifier = new MagnifierDialog();
            _magnifier.Show();
        }
        else if (_magnifier is not null && show is null or false)
        {
            _magnifier.Close();
            _magnifier = null;
        }
    }

    private void ShowWizard_Click(object sender, RoutedEventArgs e)
    {
        SetupWizard.ShowIfFirstRun(this, true);
    }
}
