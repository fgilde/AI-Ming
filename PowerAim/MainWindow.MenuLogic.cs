using Core;
using Nextended.Core.Extensions;
using PowerAim.Other;
using PowerAim.Class;
using PowerAim.Config;
using PowerAim.Localizations;
using PowerAim.UILibrary;
using PowerAim.Visuality;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using Panel = System.Windows.Controls.Panel;
using TextBox = System.Windows.Controls.TextBox;

namespace PowerAim;

public partial class MainWindow
{
    #region Menu Logic

    private string CurrentMenu = nameof(AimMenu);

    private async void MenuSwitch(object sender, RoutedEventArgs e)
    {
        if (sender is Button clickedButton && !_currentlySwitching)
        {
            var name = clickedButton.Tag?.ToString();
            if (name is not null && CurrentMenu != name)
            {
                await NavigateTo(name, true, clickedButton);
            }
        }
    }

    /// <summary>
    ///     Wire the embedded help panel's "back" button so it returns to whichever menu the user
    ///     was on before opening Help. Called once during UI bootstrap.
    /// </summary>
    private void WireHelpPanel()
    {
        if (HelpPanelHost != null)
        {
            HelpPanelHost.BackRequested -= HelpPanel_BackRequested;
            HelpPanelHost.BackRequested += HelpPanel_BackRequested;
        }
    }

    private string? _helpReturnTo;
    private void HelpPanel_BackRequested(object? sender, EventArgs e) =>
        _ = NavigateTo(_helpReturnTo ?? nameof(AimMenu));

    private Button? FindNavButton(string name)
    {
        return MenuButtons?.Children.OfType<Button>().FirstOrDefault(b => b.Tag?.ToString() == name)
            ?? MenuButtonsBottom?.Children.OfType<Button>().FirstOrDefault(b => b.Tag?.ToString() == name);
    }

    internal async Task NavigateTo(string name, bool animate = true, Button? clickedButton = null)
    {
        // Track the previously-active menu when entering the Help page so its Back button can
        // restore the user's location. Don't update on Help→Help re-entry (which would erase the
        // real back-target).
        if (name == nameof(HelpPage) && CurrentMenu != nameof(HelpPage))
            _helpReturnTo = CurrentMenu;

        if (SectionLabel is not null)
        {
            var section = string.Join(" ", name.Replace("Menu", "").SplitByUpperCase()).ToUpper();
            SectionLabel.Content = section == "AIM" ? Locale.MainSection : section;
        }

        clickedButton ??= FindNavButton(name);
        _currentlySwitching = true;
        if (clickedButton is not null && MenuHighlighter?.Parent is UIElement highlighterParent)
        {
            void Move()
            {
                try
                {
                    var transform = clickedButton.TransformToAncestor(highlighterParent);
                    var topInParent = transform.Transform(new System.Windows.Point(0, 0)).Y;
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(animate ? 220 : 0), MenuHighlighter, MenuHighlighter.Margin, new Thickness(0, topInParent, 0, 0));
                }
                catch
                {
                }
            }
            if (!clickedButton.IsLoaded || clickedButton.ActualHeight <= 0)
                clickedButton.Dispatcher.BeginInvoke(new Action(Move), System.Windows.Threading.DispatcherPriority.Loaded);
            else
                Move();
        }
        await SwitchScrollPanels(FindName(name) as FrameworkElement ?? throw new NullReferenceException("Page is null"), animate);
        CurrentMenu = name;
        // First-visit attachment: pages that started Collapsed need their visual tree walked
        // *after* they've been made visible. Wait one render tick so templates have applied.
        Dispatcher.BeginInvoke(new Action(() => EnsurePageAttached(name)),
            System.Windows.Threading.DispatcherPriority.Loaded);
        // Rebind the floating "hidden sections" pill to the new page's layout manager so it shows
        // counts for the page the user is actually looking at.
        Dispatcher.BeginInvoke(new Action(BindHiddenBoxesPillForCurrentPage),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private async Task SwitchScrollPanels(FrameworkElement movingScrollViewer, bool animate = true)
    {
        if (_currentScrollViewer is not null && _currentScrollViewer != movingScrollViewer)
        {
            _currentScrollViewer.Visibility = Visibility.Collapsed;
            _currentScrollViewer.Opacity = 1;
            _currentScrollViewer.RenderTransform = null;
        }

        movingScrollViewer.Visibility = Visibility.Visible;

        if (animate)
        {
            var translate = new System.Windows.Media.TranslateTransform(0, 8);
            movingScrollViewer.RenderTransform = translate;
            movingScrollViewer.Opacity = 0;

            var duration = TimeSpan.FromMilliseconds(180);
            var ease = new System.Windows.Media.Animation.CubicEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
            };

            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, duration) { EasingFunction = ease };
            var slideIn = new System.Windows.Media.Animation.DoubleAnimation(8, 0, duration) { EasingFunction = ease };

            movingScrollViewer.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            translate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideIn);

            await Task.Delay(duration);
            movingScrollViewer.RenderTransform = null;
        }
        else
        {
            movingScrollViewer.Opacity = 1;
            movingScrollViewer.RenderTransform = null;
        }

        _currentScrollViewer = movingScrollViewer;
        _currentlySwitching = false;
    }

    private void UnifiedSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = ((TextBox)sender).Text?.ToLower() ?? string.Empty;
        ApplySearchFilter(searchText);
    }

    private void ApplySearchFilter(string searchText)
    {
        FilterListBox(ModelListBox, searchText);
        FilterListBox(ConfigsListBox, searchText);
        FilterDownloadPanel(ModelStoreScroller, searchText);
        FilterDownloadPanel(ConfigStoreScroller, searchText);
    }

    private static void FilterListBox(System.Windows.Controls.ListBox? list, string searchText)
    {
        if (list is null) return;
        foreach (var item in list.Items)
        {
            if (list.ItemContainerGenerator.ContainerFromItem(item) is not ListBoxItem container) continue;
            var match = string.IsNullOrEmpty(searchText) || (item?.ToString()?.ToLower().Contains(searchText) ?? false);
            container.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static void FilterDownloadPanel(Panel? panel, string searchText)
    {
        if (panel is null) return;
        foreach (var item in panel.Children.OfType<ADownloadGateway>())
        {
            var match = string.IsNullOrEmpty(searchText) || (item.Title.Content?.ToString()?.ToLower().Contains(searchText) ?? false);
            item.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void SegModels_Click(object sender, RoutedEventArgs e)
    {
        SegModels.Style = (Style)FindResource("FluentSegmentActive");
        SegConfigs.Style = (Style)FindResource("FluentSegment");
        ModelsGroup.Visibility = Visibility.Visible;
        ConfigsGroup.Visibility = Visibility.Collapsed;
    }

    private void SegConfigs_Click(object sender, RoutedEventArgs e)
    {
        SegModels.Style = (Style)FindResource("FluentSegment");
        SegConfigs.Style = (Style)FindResource("FluentSegmentActive");
        ModelsGroup.Visibility = Visibility.Collapsed;
        ConfigsGroup.Visibility = Visibility.Visible;
    }

    private string? _triggerEditReturnTo;
    private PowerAim.Config.ActionTrigger? _triggerEditTarget;
    private bool _triggerEditIsNew;
    private Action<PowerAim.Config.ActionTrigger?>? _triggerEditCommit;
    private global::PowerAim.UILibrary.TriggerEdit? _triggerEditor;
    private bool _triggerEditDirty;
    private System.ComponentModel.PropertyChangedEventHandler? _triggerDirtyHandler;

    // Keeps the FOV-size sliders' max in lockstep with the active model ImageSize.

    public void OpenTriggerEditor(PowerAim.Config.ActionTrigger target, bool isNew, Action<PowerAim.Config.ActionTrigger?> commit)
    {
        _triggerEditReturnTo = CurrentMenu;
        _triggerEditTarget = target;
        _triggerEditIsNew = isNew;
        _triggerEditCommit = commit;
        if (!isNew) target.BeginEdit();
        TriggerEditTitle.Text = isNew ? Locale.AddTrigger : Locale.EditTrigger;
        TriggerEditName.Text = target.Name ?? "";
        _triggerEditDirty = false;
        TriggerEditDirty.Visibility = Visibility.Collapsed;
        if (_triggerEditor == null)
        {
            _triggerEditor = new global::PowerAim.UILibrary.TriggerEdit();
            TriggerEditorHost.Content = _triggerEditor;
        }
        _triggerEditor.Trigger = target;

        // Subscribe to property changes for dirty-tracking and live name update.
        // Use BeginInvoke so initial binding fan-out doesn't mark the form as dirty.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!ReferenceEquals(_triggerEditTarget, target)) return;
            _triggerDirtyHandler = (s, e) =>
            {
                _triggerEditDirty = true;
                TriggerEditDirty.Visibility = Visibility.Visible;
                if (e.PropertyName == nameof(PowerAim.Config.ActionTrigger.Name))
                {
                    TriggerEditName.Text = target.Name ?? "";
                }
            };
            target.PropertyChanged += _triggerDirtyHandler;
        }), System.Windows.Threading.DispatcherPriority.Background);

        SetSidebarLocked(true);
        _ = NavigateTo(nameof(TriggerEditPage));
    }

    private void CloseTriggerEditor(bool save)
    {
        var target = _triggerEditTarget;
        var isNew = _triggerEditIsNew;
        var commit = _triggerEditCommit;
        var returnTo = _triggerEditReturnTo ?? nameof(AimMenu);

        // If trying to leave without saving, warn about unsaved changes.
        if (!save && _triggerEditDirty)
        {
            var res = MessageDialog.Show(
                Locale.UnsavedChangesMessage,
                Locale.UnsavedChanges,
                MessageDialog.DialogButtons.YesNoCancel,
                MessageDialog.DialogIcon.Warning,
                owner: this,
                defaultResult: MessageDialog.DialogResult.Yes);
            if (res == MessageDialog.DialogResult.Cancel) return; // stay on editor
            if (res == MessageDialog.DialogResult.Yes) save = true; // proceed as if Save was clicked
            // No → discard (continue with save=false)
        }

        // Detach dirty handler
        if (target != null && _triggerDirtyHandler != null)
        {
            target.PropertyChanged -= _triggerDirtyHandler;
        }
        _triggerDirtyHandler = null;
        _triggerEditDirty = false;
        TriggerEditDirty.Visibility = Visibility.Collapsed;

        if (target != null && !isNew)
        {
            if (save) target.EndEdit();
            else target.CancelEdit();
        }

        SetSidebarLocked(false);
        _triggerEditTarget = null;
        _triggerEditCommit = null;
        if (_triggerEditor != null) _triggerEditor.Trigger = null!;
        commit?.Invoke(save ? target : null);
        _ = NavigateTo(returnTo);
    }

    // ===== AutoPlay profile editor (in-window page, analog to the trigger editor) =====
    private global::PowerAim.UILibrary.AutoPlayProfileEdit? _autoPlayEditor;
    private string? _autoPlayEditReturnTo;
    private PowerAim.Config.AutoPlayProfile? _autoPlayEditTarget;
    private bool _autoPlayEditIsNew;
    private Action<PowerAim.Config.AutoPlayProfile?>? _autoPlayEditCommit;
    private bool _autoPlayEditDirty;
    private System.ComponentModel.PropertyChangedEventHandler? _autoPlayDirtyHandler;

    public void OpenAutoPlayEditor(PowerAim.Config.AutoPlayProfile target, bool isNew, Action<PowerAim.Config.AutoPlayProfile?> commit)
    {
        _autoPlayEditReturnTo = CurrentMenu;
        _autoPlayEditTarget = target;
        _autoPlayEditIsNew = isNew;
        _autoPlayEditCommit = commit;
        if (!isNew) target.BeginEdit();
        AutoPlayEditTitle.Text = isNew ? Locale.AddAutoPlayProfile : Locale.EditAutoPlayProfile;
        AutoPlayEditName.Text = target.Name ?? "";
        _autoPlayEditDirty = false;
        AutoPlayEditDirty.Visibility = Visibility.Collapsed;
        if (_autoPlayEditor == null)
        {
            _autoPlayEditor = new global::PowerAim.UILibrary.AutoPlayProfileEdit();
            AutoPlayEditorHost.Content = _autoPlayEditor;
        }
        _autoPlayEditor.Profile = target;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!ReferenceEquals(_autoPlayEditTarget, target)) return;
            _autoPlayDirtyHandler = (s, e) =>
            {
                _autoPlayEditDirty = true;
                AutoPlayEditDirty.Visibility = Visibility.Visible;
                if (e.PropertyName == nameof(PowerAim.Config.AutoPlayProfile.Name))
                    AutoPlayEditName.Text = target.Name ?? "";
            };
            target.PropertyChanged += _autoPlayDirtyHandler;
        }), System.Windows.Threading.DispatcherPriority.Background);

        SetSidebarLocked(true);
        _ = NavigateTo(nameof(AutoPlayEditPage));
    }

    private void CloseAutoPlayEditor(bool save)
    {
        var target = _autoPlayEditTarget;
        var isNew = _autoPlayEditIsNew;
        var commit = _autoPlayEditCommit;
        var returnTo = _autoPlayEditReturnTo ?? nameof(AutoPlayMenu);

        if (!save && _autoPlayEditDirty)
        {
            var res = MessageDialog.Show(
                Locale.UnsavedChangesMessage,
                Locale.UnsavedChanges,
                MessageDialog.DialogButtons.YesNoCancel,
                MessageDialog.DialogIcon.Warning,
                owner: this,
                defaultResult: MessageDialog.DialogResult.Yes);
            if (res == MessageDialog.DialogResult.Cancel) return;
            if (res == MessageDialog.DialogResult.Yes) save = true;
        }

        if (target != null && _autoPlayDirtyHandler != null)
            target.PropertyChanged -= _autoPlayDirtyHandler;
        _autoPlayDirtyHandler = null;
        _autoPlayEditDirty = false;
        AutoPlayEditDirty.Visibility = Visibility.Collapsed;

        if (target != null && !isNew)
        {
            if (save) target.EndEdit();
            else target.CancelEdit();
        }

        SetSidebarLocked(false);
        _autoPlayEditTarget = null;
        _autoPlayEditCommit = null;
        // Intentionally NOT clearing _autoPlayEditor.Profile: the panel's UpdateDynamicUi binds to
        // Profile.DecisionInterval without a null-guard, so null would throw. The next open replaces it.
        commit?.Invoke(save ? target : null);
        _ = NavigateTo(returnTo);
    }

    private void AutoPlayEditBack_Click(object sender, RoutedEventArgs e) => CloseAutoPlayEditor(false);
    private void AutoPlayEditCancel_Click(object sender, RoutedEventArgs e) => CloseAutoPlayEditor(false);
    private void AutoPlayEditSave_Click(object sender, RoutedEventArgs e) => CloseAutoPlayEditor(true);

    // ===== AntiRecoil profile editor (in-window page, analog to TriggerEditPage / AutoPlayEditPage) =====
    private global::PowerAim.UILibrary.AntiRecoilProfileEdit? _antiRecoilEditor;
    private string? _antiRecoilEditReturnTo;
    private PowerAim.Config.AntiRecoilProfile? _antiRecoilEditTarget;
    private bool _antiRecoilEditIsNew;
    private Action<PowerAim.Config.AntiRecoilProfile?>? _antiRecoilEditCommit;
    private bool _antiRecoilEditDirty;
    private System.ComponentModel.PropertyChangedEventHandler? _antiRecoilDirtyHandler;

    /// <summary>
    ///     Open the AntiRecoil profile editor in-window (Page) instead of a modal dialog. Mirrors
    ///     <see cref="OpenAutoPlayEditor"/> exactly: BeginEdit on existing profiles so Cancel
    ///     rolls back, dirty-flag tracking for the unsaved-changes prompt, sidebar locked while
    ///     editing.
    /// </summary>
    public void OpenAntiRecoilEditor(PowerAim.Config.AntiRecoilProfile target, bool isNew, Action<PowerAim.Config.AntiRecoilProfile?> commit)
    {
        _antiRecoilEditReturnTo = CurrentMenu;
        _antiRecoilEditTarget = target;
        _antiRecoilEditIsNew = isNew;
        _antiRecoilEditCommit = commit;
        if (!isNew) target.BeginEdit();
        AntiRecoilEditTitle.Text = isNew ? Locale.AntiRecoilAddProfile : Locale.AntiRecoilProfileEdit;
        AntiRecoilEditName.Text = target.Name ?? "";
        _antiRecoilEditDirty = false;
        AntiRecoilEditDirty.Visibility = Visibility.Collapsed;
        if (_antiRecoilEditor == null)
        {
            _antiRecoilEditor = new global::PowerAim.UILibrary.AntiRecoilProfileEdit();
            AntiRecoilEditorHost.Content = _antiRecoilEditor;
        }
        _antiRecoilEditor.Profile = target;

        // Dirty-tracking via PropertyChanged — same shape as AutoPlay.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!ReferenceEquals(_antiRecoilEditTarget, target)) return;
            _antiRecoilDirtyHandler = (s, e) =>
            {
                _antiRecoilEditDirty = true;
                AntiRecoilEditDirty.Visibility = Visibility.Visible;
                if (e.PropertyName == nameof(PowerAim.Config.AntiRecoilProfile.Name))
                {
                    AntiRecoilEditName.Text = target.Name ?? "";
                }
            };
            target.PropertyChanged += _antiRecoilDirtyHandler;
        }), System.Windows.Threading.DispatcherPriority.Background);

        SetSidebarLocked(true);
        _ = NavigateTo(nameof(AntiRecoilEditPage));
    }

    private void CloseAntiRecoilEditor(bool save)
    {
        var target = _antiRecoilEditTarget;
        var isNew = _antiRecoilEditIsNew;
        var commit = _antiRecoilEditCommit;
        var returnTo = _antiRecoilEditReturnTo ?? nameof(AimMenu);

        if (!save && _antiRecoilEditDirty)
        {
            var res = MessageDialog.Show(
                Locale.UnsavedChangesMessage, Locale.UnsavedChanges,
                MessageDialog.DialogButtons.YesNoCancel,
                MessageDialog.DialogIcon.Warning,
                owner: this,
                defaultResult: MessageDialog.DialogResult.Yes);
            if (res == MessageDialog.DialogResult.Cancel) return;
            if (res == MessageDialog.DialogResult.Yes) save = true;
        }

        if (target != null && _antiRecoilDirtyHandler != null)
            target.PropertyChanged -= _antiRecoilDirtyHandler;
        _antiRecoilDirtyHandler = null;
        _antiRecoilEditDirty = false;
        AntiRecoilEditDirty.Visibility = Visibility.Collapsed;

        if (target != null && !isNew)
        {
            if (save) target.EndEdit();
            else target.CancelEdit();
        }

        SetSidebarLocked(false);
        _antiRecoilEditTarget = null;
        _antiRecoilEditCommit = null;
        commit?.Invoke(save ? target : null);
        _ = NavigateTo(returnTo);
    }

    private void AntiRecoilEditBack_Click(object sender, RoutedEventArgs e)   => CloseAntiRecoilEditor(false);
    private void AntiRecoilEditCancel_Click(object sender, RoutedEventArgs e) => CloseAntiRecoilEditor(false);
    private void AntiRecoilEditSave_Click(object sender, RoutedEventArgs e)   => CloseAntiRecoilEditor(true);

    // ===== Aim profile editor (in-window page, analog to AntiRecoilEditPage) =====
    private global::PowerAim.UILibrary.AimProfileEdit? _aimEditor;
    private string? _aimEditReturnTo;
    private PowerAim.Config.AimProfile? _aimEditTarget;
    private bool _aimEditIsNew;
    private Action<PowerAim.Config.AimProfile?>? _aimEditCommit;
    private bool _aimEditDirty;
    private System.ComponentModel.PropertyChangedEventHandler? _aimDirtyHandler;

    /// <summary>Open the aim-profile editor in-window. Mirrors <see cref="OpenAntiRecoilEditor"/>.</summary>
    public void OpenAimEditor(PowerAim.Config.AimProfile target, bool isNew, Action<PowerAim.Config.AimProfile?> commit)
    {
        _aimEditReturnTo = CurrentMenu;
        _aimEditTarget = target;
        _aimEditIsNew = isNew;
        _aimEditCommit = commit;
        if (!isNew) target.BeginEdit();
        AimEditTitle.Text = isNew ? Locale.AimAddProfile : Locale.AimProfileEdit;
        AimEditName.Text = target.Name ?? "";
        _aimEditDirty = false;
        AimEditDirty.Visibility = Visibility.Collapsed;
        if (_aimEditor == null)
        {
            _aimEditor = new global::PowerAim.UILibrary.AimProfileEdit();
            AimEditorHost.Content = _aimEditor;
        }
        _aimEditor.Profile = target;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!ReferenceEquals(_aimEditTarget, target)) return;
            _aimDirtyHandler = (s, e) =>
            {
                _aimEditDirty = true;
                AimEditDirty.Visibility = Visibility.Visible;
                if (e.PropertyName == nameof(PowerAim.Config.AimProfile.Name))
                    AimEditName.Text = target.Name ?? "";
            };
            target.PropertyChanged += _aimDirtyHandler;
        }), System.Windows.Threading.DispatcherPriority.Background);

        SetSidebarLocked(true);
        _ = NavigateTo(nameof(AimEditPage));
    }

    private void CloseAimEditor(bool save)
    {
        var target = _aimEditTarget;
        var isNew = _aimEditIsNew;
        var commit = _aimEditCommit;
        var returnTo = _aimEditReturnTo ?? nameof(AimMenu);

        if (!save && _aimEditDirty)
        {
            var res = MessageDialog.Show(
                Locale.UnsavedChangesMessage, Locale.UnsavedChanges,
                MessageDialog.DialogButtons.YesNoCancel,
                MessageDialog.DialogIcon.Warning,
                owner: this,
                defaultResult: MessageDialog.DialogResult.Yes);
            if (res == MessageDialog.DialogResult.Cancel) return;
            if (res == MessageDialog.DialogResult.Yes) save = true;
        }

        if (target != null && _aimDirtyHandler != null)
            target.PropertyChanged -= _aimDirtyHandler;
        _aimDirtyHandler = null;
        _aimEditDirty = false;
        AimEditDirty.Visibility = Visibility.Collapsed;

        if (target != null && !isNew)
        {
            if (save) target.EndEdit();
            else target.CancelEdit();
        }

        SetSidebarLocked(false);
        _aimEditTarget = null;
        _aimEditCommit = null;
        commit?.Invoke(save ? target : null);

        // If the edited profile is the one currently DRIVING the aim, re-apply its (possibly changed)
        // values to the live settings the pipeline reads (the per-frame resolver only re-applies on a
        // profile-id change, not on value edits).
        if (save && target != null)
            AILogic.AimProfileManager.Instance.ReapplyIfEffective(target);

        _ = NavigateTo(returnTo);
    }

    private void AimEditBack_Click(object sender, RoutedEventArgs e)   => CloseAimEditor(false);
    private void AimEditCancel_Click(object sender, RoutedEventArgs e) => CloseAimEditor(false);
    private void AimEditSave_Click(object sender, RoutedEventArgs e)   => CloseAimEditor(true);

    private void SetSidebarLocked(bool locked)
    {
        if (Sidebar is not null)
        {
            Sidebar.IsHitTestVisible = !locked;
            Sidebar.Opacity = locked ? 0.4 : 1.0;
        }
        if (HamburgerButton is not null) HamburgerButton.IsEnabled = !locked;
    }

    private void TriggerEditBack_Click(object sender, RoutedEventArgs e) => CloseTriggerEditor(false);
    private void TriggerEditCancel_Click(object sender, RoutedEventArgs e) => CloseTriggerEditor(false);
    private void TriggerEditSave_Click(object sender, RoutedEventArgs e) => CloseTriggerEditor(true);

    // ============================================================================ MAPPING EDITOR ====

    private string? _mappingEditReturnTo;
    private PowerAim.Config.ControllerMappingProfile? _mappingEditTarget;
    private bool _mappingEditDirty;
    private bool _mappingEditIsNew;
    private Action<PowerAim.Config.ControllerMappingProfile?>? _mappingEditCommit;
    private System.ComponentModel.PropertyChangedEventHandler? _mappingDirtyHandler;
    private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _mappingCollectionDirtyHandler;
    private List<PowerAim.Config.InputMapping>? _mappingEditSnapshot;

    /// <summary>
    ///     True while the mapping editor is open. The <see cref="MappingPage"/>'s
    ///     auto-save handlers check this to skip writing during editing — otherwise every keystroke
    ///     would persist the in-flight state and Discard would have nothing to roll back to.
    /// </summary>
    public bool IsMappingEditorOpen { get; private set; }

    /// <summary>
    ///     Open the mapping editor.
    ///     <para>
    ///     <paramref name="isNew"/> = true: <paramref name="target"/> is a draft NOT in the
    ///     profiles collection yet. <paramref name="commit"/> is invoked with the saved profile
    ///     (or null on discard). This is the trigger-editor pattern — prevents zombie "Profile N"
    ///     entries from clicking + immediately discarding.
    ///     </para>
    ///     <para>
    ///     <paramref name="isNew"/> = false: target is already in the collection. We snapshot its
    ///     mappings list + call BeginEdit; on Discard we restore from the snapshot AND call
    ///     CancelEdit so both property changes and inner mappings-list mutations roll back.
    ///     </para>
    /// </summary>
    public void OpenMappingEditor(
        PowerAim.Config.ControllerMappingProfile target,
        bool isNew = false,
        Action<PowerAim.Config.ControllerMappingProfile?>? commit = null)
    {
        _mappingEditReturnTo = CurrentMenu;
        _mappingEditTarget = target;
        _mappingEditIsNew = isNew;
        _mappingEditCommit = commit;
        IsMappingEditorOpen = true;

        if (!isNew)
        {
            // For existing profiles: take a snapshot of the current Mappings list so Discard can
            // restore it. Nextended's BeginEdit handles primitive properties; the inner collection
            // mutations are NOT covered by IEditableObject contract.
            _mappingEditSnapshot = target.Mappings.Select(ClonedMapping).ToList();
            target.BeginEdit();
        }
        else
        {
            _mappingEditSnapshot = null;
        }

        MappingEditName.Text = target.Name ?? "";
        _mappingEditDirty = isNew; // new drafts start "dirty" so save is the only way to commit
        MappingEditDirty.Visibility = _mappingEditDirty ? Visibility.Visible : Visibility.Collapsed;

        MappingEditor.Profile = target;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!ReferenceEquals(_mappingEditTarget, target)) return;
            _mappingDirtyHandler = (s, e) =>
            {
                _mappingEditDirty = true;
                MappingEditDirty.Visibility = Visibility.Visible;
                if (e.PropertyName == nameof(PowerAim.Config.ControllerMappingProfile.Name))
                    MappingEditName.Text = target.Name ?? "";
            };
            target.PropertyChanged += _mappingDirtyHandler;

            _mappingCollectionDirtyHandler = (s, e) =>
            {
                _mappingEditDirty = true;
                MappingEditDirty.Visibility = Visibility.Visible;
            };
            target.Mappings.CollectionChanged += _mappingCollectionDirtyHandler;
        }), System.Windows.Threading.DispatcherPriority.Background);

        SetSidebarLocked(true);
        _ = NavigateTo(nameof(MappingEditPage));
    }

    private static PowerAim.Config.InputMapping ClonedMapping(PowerAim.Config.InputMapping m) => new()
    {
        SourceKind = m.SourceKind,
        SourceCode = m.SourceCode,
        TargetKind = m.TargetKind,
        TargetCode = m.TargetCode,
        Enabled = m.Enabled,
        Activator = m.Activator,
        LongPressMs = m.LongPressMs,
        ModifierKind = m.ModifierKind,
        ModifierCode = m.ModifierCode,
    };

    private void CloseMappingEditor(bool save)
    {
        var target = _mappingEditTarget;
        var returnTo = _mappingEditReturnTo ?? nameof(MappingMenu);
        var isNew = _mappingEditIsNew;
        var commit = _mappingEditCommit;
        var snapshot = _mappingEditSnapshot;

        if (!save && _mappingEditDirty)
        {
            var res = MessageDialog.Show(
                Locale.UnsavedChangesMessage,
                Locale.UnsavedChanges,
                MessageDialog.DialogButtons.YesNoCancel,
                MessageDialog.DialogIcon.Warning,
                owner: this,
                defaultResult: MessageDialog.DialogResult.Yes);
            if (res == MessageDialog.DialogResult.Cancel) return;
            if (res == MessageDialog.DialogResult.Yes) save = true;
        }

        if (target != null)
        {
            if (_mappingDirtyHandler != null)
                target.PropertyChanged -= _mappingDirtyHandler;
            if (_mappingCollectionDirtyHandler != null)
                target.Mappings.CollectionChanged -= _mappingCollectionDirtyHandler;
            if (save)
            {
                if (!isNew) target.EndEdit();
                // Hand draft to the commit callback (which appends to collection for new
                // profiles) BEFORE writing to disk so the JSON contains it.
                commit?.Invoke(target);
                AppConfig.Current?.Save();
            }
            else
            {
                if (!isNew)
                {
                    // Roll back inner mappings list to the snapshot (IEditableObject doesn't cover
                    // ObservableCollection mutations), then roll back primitive properties.
                    if (snapshot != null)
                    {
                        target.Mappings.Clear();
                        foreach (var m in snapshot)
                            target.Mappings.Add(m);
                    }
                    target.CancelEdit();
                }
                // For isNew drafts: do nothing — the profile was never in the collection.
                commit?.Invoke(null);
            }
        }
        _mappingDirtyHandler = null;
        _mappingCollectionDirtyHandler = null;
        _mappingEditDirty = false;
        _mappingEditSnapshot = null;
        _mappingEditCommit = null;
        _mappingEditIsNew = false;
        MappingEditDirty.Visibility = Visibility.Collapsed;
        MappingEditor.Profile = null;
        _mappingEditTarget = null;
        IsMappingEditorOpen = false;

        SetSidebarLocked(false);
        _ = NavigateTo(returnTo);
    }

    private void MappingEditBack_Click(object sender, RoutedEventArgs e) => CloseMappingEditor(false);
    private void MappingEditCancel_Click(object sender, RoutedEventArgs e) => CloseMappingEditor(false);
    private void MappingEditSave_Click(object sender, RoutedEventArgs e) => CloseMappingEditor(true);

    private async Task RunBenchmarkClick()
    {
        var modelFile = AppConfig.Current?.LastLoadedModel;
        if (string.IsNullOrWhiteSpace(modelFile) || modelFile == "N/A")
        {
            MessageDialog.Warn(Locale.BenchmarkNoModel, Locale.RunBenchmark, owner: this);
            return;
        }
        var modelPath = Path.Combine(Constants.ModelsBasePath, modelFile);
        if (!File.Exists(modelPath))
        {
            MessageDialog.Warn(Locale.BenchmarkNoModel, Locale.RunBenchmark, owner: this);
            return;
        }

        var notice = new NoticeBar(Locale.BenchmarkRunning, 60000);
        notice.Show();
        try
        {
            var result = await PowerAim.AILogic.PerformanceBenchmark.RunAsync(modelPath);
            notice.Close();

            var msg = Locale.BenchmarkRecommendedSize.FormatWith(result.RecommendedImageSize) + "\n\n";
            foreach (var s in result.Samples)
            {
                msg += $"• {s.ImageSize}px → {s.AvgFps:F1} fps ({s.AvgInferenceMs:F1} ms";
                if (s.GpuUtilizationPct > 0) msg += $", GPU {s.GpuUtilizationPct:F0}%";
                msg += ")\n";
            }
            if (!string.IsNullOrWhiteSpace(result.Notes)) msg += "\n" + result.Notes;

            MessageDialog.Show(
                msg, Locale.BenchmarkResult,
                MessageDialog.DialogButtons.OK,
                MessageDialog.DialogIcon.Info,
                owner: this);
        }
        catch (Exception ex)
        {
            notice.Close();
            MessageDialog.Error(ex.Message, Locale.RunBenchmark, owner: this);
        }
    }


    private void BindingOnKeyReleased(string bindingId)
    {
        switch (bindingId)
        {
            case nameof(AppConfig.Current.BindingSettings.DynamicFOVKeybind):
                AppConfig.Current.SliderSettings.OnPropertyChanged(nameof(AppConfig.Current.SliderSettings.ActualFovSize));
                if (FOV.Instance is not null)
                {
                    Animator.WidthShift(TimeSpan.FromMilliseconds(500), FOV.Instance.Circle, FOV.Instance.Circle.ActualWidth, AppConfig.Current.SliderSettings.FOVSize);
                    Animator.HeightShift(TimeSpan.FromMilliseconds(500), FOV.Instance.Circle, FOV.Instance.Circle.ActualHeight, AppConfig.Current.SliderSettings.FOVSize);
                }
                break;
        }
    }

    private void BindingOnKeyPressed(string bindingId)
    {
        if (AppConfig.Current?.ToggleState is { RequireGlobalActiveForKeybinds: true, GlobalActive: false })
            return;

        switch (bindingId)
        {
            case nameof(AppConfig.Current.BindingSettings.MagnifierZoomInKeybind):
                AppConfig.Current.SliderSettings.MagnificationFactor += AppConfig.Current.SliderSettings.MagnificationStepFactor;
                ValidateMagnificationFactor();
                break;
            case nameof(AppConfig.Current.BindingSettings.MagnifierZoomOutKeybind):
                AppConfig.Current.SliderSettings.MagnificationFactor -= AppConfig.Current.SliderSettings.MagnificationStepFactor;
                ValidateMagnificationFactor();
                break;
            case nameof(AppConfig.Current.BindingSettings.MagnifierKeybind):
                ToggleMagnifier();
                break;
            case nameof(AppConfig.Current.BindingSettings.ModelSwitchKeybind):
                if (AppConfig.Current.BindingSettings.ModelSwitchKeybind.IsValid)
                    if (!FileManager.CurrentlyLoadingModel)
                    {
                        if (ModelListBox.SelectedIndex >= 0 &&
                            ModelListBox.SelectedIndex < ModelListBox.Items.Count - 1)
                            ModelListBox.SelectedIndex += 1;
                        else
                            ModelListBox.SelectedIndex = 0;
                    }

                break;

            case nameof(AppConfig.Current.BindingSettings.DynamicFOVKeybind):
                AppConfig.Current.SliderSettings.OnPropertyChanged(nameof(AppConfig.Current.SliderSettings.ActualFovSize));
                if (FOV.Instance is not null)
                {
                    Animator.WidthShift(TimeSpan.FromMilliseconds(500), FOV.Instance.Circle, FOV.Instance.Circle.ActualWidth, AppConfig.Current.SliderSettings.ActualFovSize);
                    Animator.HeightShift(TimeSpan.FromMilliseconds(500), FOV.Instance.Circle, FOV.Instance.Circle.ActualHeight, AppConfig.Current.SliderSettings.ActualFovSize);
                }

                break;


            case nameof(AppConfig.Current.BindingSettings.DisableAntiRecoilKeybind):
                if (AppConfig.Current.ToggleState.AntiRecoil)
                {
                    AppConfig.Current.ToggleState.AntiRecoil = false;
                    new NoticeBar(Locale.DisableAntiRecoilKeybindExt, 4000).Show();
                }

                break;

            // Gun1Key / Gun2Key cases removed: the old "load gun-config file" behaviour is
            // superseded by the AntiRecoilProfile keybind activation (see AntiRecoilProfileManager
            // — each profile's KeyBind toggles ActiveProfileId). Binding fields remain on
            // BindingSettings for legacy compat but are unused.
        }
    }

    #endregion Menu Logic
}
