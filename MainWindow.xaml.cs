using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Services;
using BetterCM.Views;
namespace BetterCM;

public sealed class ActionRow
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Shortcut { get; init; } = "";
    public bool Enabled { get; set; }
    public bool IsPrimary { get; set; }
    public bool CanPin { get; init; }
}
public sealed class ShellRow
{
    public string Key { get; init; } = "";
    public string Title { get; init; } = "";
    public bool Pinned { get; set; }
    public bool Hidden { get; set; }
}
public partial class MainWindow : Window
{
    private readonly App _app;
    private readonly ObservableCollection<ActionRow> _actions = [];
    private readonly ObservableCollection<CustomActionItem> _custom = [];
    private readonly ObservableCollection<ShellRow> _shell = [];
    private string _loadedConfig = "";
    private bool _configurationConflict;
    public MainWindow(App app) { _app = app; InitializeComponent(); Title = AppInfo.DisplayName + " · 设置"; ThemeService.Attach(this); BuiltinGrid.ItemsSource = _actions; CustomGrid.ItemsSource = _custom; ShellGrid.ItemsSource = _shell; RefreshSettings(); }
    public void RefreshSettings()
    {
        var config = _app.Config.Clone();
        _loadedConfig = ConfigJson.Snapshot(config);
        _configurationConflict = false;
        ClassicOption.IsChecked = config.ClassicMenu;
        RefreshModeAvailability();
        ReplaceMenu.IsChecked = config.ReplaceExplorerMenu; StartWithWindows.IsChecked = StartupService.Enabled;
        ShellActions.IsChecked = config.IncludeShellActions; FileDetails.IsChecked = config.ShowFileDetails;
        Animations.IsChecked = config.EnableAnimations; Notifications.IsChecked = config.ShowNotifications;
        HotkeyText.Text = config.GlobalHotkey;
        ThemeChoice.SelectedIndex = config.Theme switch { "dark" => 1, "light" => 2, _ => 0 };
        SelectChoice(MaterialChoice, config.Material); ShowMoreOption.IsChecked = config.ShowMoreItems; CompactOption.IsChecked = config.CompactMenu; MoreDelay.Value = config.MoreHoverDelayMs;
        SubmenuDelay.Value = config.SubmenuHoverDelayMs;
        LoadMotion(config);
        _actions.Clear();
        foreach (var definition in ActionCatalog.All.OrderBy(x => ActionCatalog.Preference(config, x).Order))
        {
            var pref = ActionCatalog.Preference(config, definition);
            _actions.Add(new() { Id = definition.Id, Title = definition.Title + (definition.Toolbar ? " · 顶部工具栏" : ""), Shortcut = definition.Shortcut, Enabled = pref.Enabled, IsPrimary = definition.Toolbar || pref.IsPrimary, CanPin = !definition.Toolbar });
        }
        _custom.Clear(); foreach (var custom in config.CustomActions) _custom.Add(custom);
        _shell.Clear(); foreach (var item in _app.LastShellItems.DistinctBy(x => x.Key)) _shell.Add(new() { Key = item.Key, Title = item.Group.Length == 0 ? item.Title : item.Group + " · " + item.Title, Pinned = config.PinnedShellActions.Contains(item.Key), Hidden = config.HiddenShellActions.Contains(item.Key) });
        RefreshDiagnostics(); SaveStatus.Text = "设置保存后生效";
    }
    public void RefreshDiagnostics()
    {
        InputStatus.Text = _app.InputStatus;
        var ctx = _app.LastContext;
        DiagnosticsText.Text = ctx == null ? "尚未捕获上下文。可在资源管理器选中文件后右键，或按呼出快捷键。\n\n配置：" + _app.ConfigPath : $"捕获时间：{ctx.CapturedAt:HH:mm:ss}\n来源：{ctx.Source}\n数量：{ctx.Items.Count}\n目录：{ctx.CurrentDirectory}\n目标：{string.Join("\n", ctx.TargetPaths)}\n\n{_app.Timing}\n配置：{_app.ConfigPath}";
        DiagnosticsText.Text += "\n\n" + _app.MenuDiagnostics;
    }
    private static void SelectChoice(ComboBox choice, string value) => choice.SelectedItem = choice.Items.Cast<ComboBoxItem>().First(x => x.Tag.ToString() == value);
    private static string Choice(ComboBox choice) => ((ComboBoxItem)choice.SelectedItem).Tag.ToString()!;
    private void LoadMotion(AppConfig config)
    {
        Animations.IsChecked = config.EnableAnimations;
        SelectChoice(OpenStyle, config.OpenAnimation); SelectChoice(CloseStyle, config.CloseAnimation); SelectChoice(MotionEasing, config.AnimationEasing);
        OpenDuration.Value = config.OpenAnimationMs; CloseDuration.Value = config.CloseAnimationMs; MotionDistance.Value = config.AnimationDistance; MotionScale.Value = config.AnimationScalePercent;
        ExpansionAnimations.IsChecked = config.EnableExpansionAnimations;
        SelectChoice(MoreExpansionStyle, config.MoreExpansionAnimation); SelectChoice(GroupExpansionStyle, config.GroupExpansionAnimation);
        MoreExpansionDuration.Value = config.MoreExpansionMs; GroupExpansionDuration.Value = config.GroupExpansionMs;
    }
    private void ResetMotion_Click(object sender, RoutedEventArgs e) { LoadMotion(new()); SaveStatus.Text = "已恢复默认动效；保存后生效"; }
    private AppConfig ReadSettings()
    {
        BuiltinGrid.CommitEdit(); CustomGrid.CommitEdit(); ShellGrid.CommitEdit();
        var config = _app.Config.Clone();
        config.GlobalHotkey = HotkeyText.Text.Trim(); config.ReplaceExplorerMenu = ReplaceMenu.IsChecked == true;
        config.IncludeShellActions = ShellActions.IsChecked == true; config.ShowFileDetails = FileDetails.IsChecked == true;
        config.ClassicMenu = ClassicOption.IsChecked == true;
        config.EnableAnimations = Animations.IsChecked == true; config.ShowNotifications = Notifications.IsChecked == true;
        config.Theme = Choice(ThemeChoice); config.Material = Choice(MaterialChoice);
        config.ShowMoreItems = ShowMoreOption.IsChecked == true; config.MoreHoverDelayMs = (int)Math.Round(MoreDelay.Value);
        config.CompactMenu = CompactOption.IsChecked == true;
        config.SubmenuHoverDelayMs = (int)Math.Round(SubmenuDelay.Value);
        config.OpenAnimation = Choice(OpenStyle); config.CloseAnimation = Choice(CloseStyle); config.AnimationEasing = Choice(MotionEasing);
        config.OpenAnimationMs = (int)Math.Round(OpenDuration.Value); config.CloseAnimationMs = (int)Math.Round(CloseDuration.Value);
        config.AnimationDistance = (int)Math.Round(MotionDistance.Value); config.AnimationScalePercent = (int)Math.Round(MotionScale.Value);
        config.EnableExpansionAnimations = ExpansionAnimations.IsChecked == true;
        config.MoreExpansionAnimation = Choice(MoreExpansionStyle); config.GroupExpansionAnimation = Choice(GroupExpansionStyle);
        config.MoreExpansionMs = (int)Math.Round(MoreExpansionDuration.Value); config.GroupExpansionMs = (int)Math.Round(GroupExpansionDuration.Value);
        config.Actions = _actions.Select((x, i) => new ActionPreference { Id = x.Id, Enabled = x.Enabled, IsPrimary = x.IsPrimary, Order = i }).ToList();
        config.CustomActions = _custom.ToList();
        foreach (var row in _shell) { if (row.Pinned) config.PinnedShellActions.Add(row.Key); else config.PinnedShellActions.Remove(row.Key); if (row.Hidden) config.HiddenShellActions.Add(row.Key); else config.HiddenShellActions.Remove(row.Key); }
        return config;
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_loadedConfig != ConfigJson.Snapshot(_app.Config))
        {
            NotifyConfigurationChanged(); return;
        }
        try
        {
            var error = _app.SaveSettings(ReadSettings(), StartWithWindows.IsChecked == true);
            if (error == null) RefreshSettings();
            SaveStatus.Text = error ?? "设置已保存";
        }
        catch (Exception ex) { SaveStatus.Text = ex.Message; }
        RefreshDiagnostics();
    }
    public void NotifyConfigurationChanged()
    {
        RefreshDiagnostics();
        _configurationConflict = _loadedConfig != ConfigJson.Snapshot(_app.Config);
        if (_configurationConflict)
            SaveStatus.Text = "配置已在外部更新。未保存的编辑已保留；请点击“还原为已保存”加载最新配置后再保存。";
        SaveButton.IsEnabled = !_configurationConflict && HotkeyValidation.Text.StartsWith("格式正确", StringComparison.Ordinal);
    }
    private void Classic_Changed(object sender, RoutedEventArgs e)
        => RefreshModeAvailability();
    public void RefreshModeAvailability()
    {
        if (ShellActions == null || ShowMoreOption == null || BuiltinGrid == null || CustomGrid == null || ShellGrid == null) return;
        bool enhanced = ClassicOption.IsChecked != true;
        ShellActions.IsEnabled = ShowMoreOption.IsEnabled = BuiltinGrid.IsEnabled = CustomGrid.IsEnabled = ShellGrid.IsEnabled = enhanced;
        MoreDelay.IsEnabled = enhanced;
        BuiltinCommands.IsEnabled = CustomCommands.IsEnabled = enhanced;
    }
    private void Hotkey_Changed(object sender, TextChangedEventArgs e)
    {
        if (HotkeyValidation == null || SaveButton == null) return;
        string? error = null;
        try { Hotkey.Parse(HotkeyText.Text.Trim()); } catch (ArgumentException ex) { error = ex.Message; }
        HotkeyValidation.Text = error ?? "格式正确；是否被占用将在保存时检查";
        HotkeyValidation.SetResourceReference(TextBlock.ForegroundProperty, error == null ? "MutedBrush" : "ErrorBrush");
        HotkeyText.SetResourceReference(Control.BorderBrushProperty, error == null ? "BorderBrush" : "ErrorBrush");
        SaveButton.IsEnabled = error == null && !_configurationConflict;
    }
    private void RestoreSaved_Click(object sender, RoutedEventArgs e)
    {
        foreach (var grid in new[] { BuiltinGrid, CustomGrid, ShellGrid }) { grid.CancelEdit(DataGridEditingUnit.Cell); grid.CancelEdit(DataGridEditingUnit.Row); }
        _app.CancelPreview(); RefreshSettings(); SaveStatus.Text = "已还原为已保存设置，未保存的修改已取消";
    }
    private void MoveUp_Click(object sender, RoutedEventArgs e) => Move(-1);
    private void MoveDown_Click(object sender, RoutedEventArgs e) => Move(1);
    private void Move(int direction) { if (BuiltinGrid.SelectedItem is ActionRow row) { int current = _actions.IndexOf(row), target = current + direction; if (target >= 0 && target < _actions.Count) { _actions.Move(current, target); BuiltinGrid.SelectedItem = row; } } }
    private void AddCustom_Click(object sender, RoutedEventArgs e) { var dialog = new CustomActionWindow(new()) { Owner = this }; if (dialog.ShowDialog() == true) _custom.Add(dialog.Result); }
    private void EditCustom_Click(object sender, RoutedEventArgs e) { if (CustomGrid.SelectedItem is CustomActionItem item) { var dialog = new CustomActionWindow(item) { Owner = this }; if (dialog.ShowDialog() == true) _custom[_custom.IndexOf(item)] = dialog.Result; } }
    private void RemoveCustom_Click(object sender, RoutedEventArgs e) { if (CustomGrid.SelectedItem is CustomActionItem item) _custom.Remove(item); }
    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        try { _app.PreviewWithOptions(ReadSettings()); SaveStatus.Text = "正在预览当前选项，关闭菜单后恢复已保存的外观"; }
        catch (Exception ex) { SaveStatus.Text = ex.Message; }
    }
    private void OpenConfig_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo(_app.ConfigPath) { UseShellExecute = true });
    private void OpenLogs_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo(ConfigStore.DataDirectory) { UseShellExecute = true });
    private void Hide_Click(object sender, RoutedEventArgs e) => Hide();
    private void Window_Closing(object? sender, CancelEventArgs e) { if (!_app.IsExiting) { e.Cancel = true; Hide(); } }
}
