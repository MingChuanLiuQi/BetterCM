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
    public string Group { get; set; } = "";
    public ActionRule Rule { get; set; } = new();
}
public sealed class ShellRow
{
    public string Key { get; init; } = "";
    public string Title { get; set; } = "";
    public string Group { get; set; } = "";
    public bool Pinned { get; set; }
    public bool Hidden { get; set; }
}
public partial class MainWindow : Window
{
    private readonly App _app;
    private readonly ObservableCollection<ActionRow> _actions = [];
    private readonly ObservableCollection<CustomActionItem> _custom = [];
    private readonly ObservableCollection<ShellRow> _shell = [];
    private readonly Dictionary<string, ShellRow> _shellEdits = new(StringComparer.Ordinal);
    private string _loadedConfig = "";
    private AppConfig _loadedSettings = new();
    private bool _loadedStartup;
    private bool _configurationConflict;
    private bool _validHotkey;
    private bool _customTransferBusy;
    private readonly MenuWorkspaceEditor _workspace = new();
    public MainWindow(App app) { _app = app; InitializeComponent(); WorkspaceHost.Content = _workspace; SettingsWindowSizing.Attach(this); Title = AppInfo.DisplayName + " · 设置"; ThemeService.Attach(this); BuiltinGrid.ItemsSource = _actions; CustomGrid.ItemsSource = _custom; ShellGrid.ItemsSource = _shell; RefreshSettings(); }
    public void RefreshSettings()
        => LoadSettings(_app.Config.Clone(), StartupService.Enabled);
    internal bool HasUnsavedChanges
        => _configurationConflict || !_validHotkey || (StartWithWindows.IsChecked == true) != _loadedStartup || ConfigJson.Snapshot(ReadSettings()) != _loadedConfig;
    public void PrepareForShow()
    {
        if (!HasUnsavedChanges)
        {
            RefreshSettings();
            return;
        }
        RefreshShellItems();
        NotifyConfigurationChanged();
        if (!_configurationConflict)
            SaveStatus.Text = "已保留上次未保存的修改，点击“保存设置”生效，或点击“撤销未保存的修改”还原。";
    }
    private void LoadSettings(AppConfig config, bool startup)
    {
        _loadedSettings = _app.Config.Clone();
        _loadedConfig = ConfigJson.Snapshot(_loadedSettings);
        _loadedStartup = StartupService.Enabled;
        _configurationConflict = false;
        ClassicOption.IsChecked = config.ClassicMenu;
        RefreshModeAvailability();
        ReplaceMenu.IsChecked = config.ReplaceExplorerMenu; StartWithWindows.IsChecked = startup;
        ShellActions.IsChecked = config.IncludeShellActions; FileDetails.IsChecked = config.ShowFileDetails;
        Animations.IsChecked = config.EnableAnimations; Notifications.IsChecked = config.ShowNotifications;
        HotkeyText.Text = config.GlobalHotkey;
        SelectChoice(ThemeChoice, config.Theme);
        SelectChoice(MaterialChoice, config.Material); ShowMoreOption.IsChecked = config.ShowMoreItems; CompactOption.IsChecked = config.CompactMenu; MoreDelay.Value = config.MoreHoverDelayMs;
        SubmenuDelay.Value = config.SubmenuHoverDelayMs;
        LoadMotion(config);
        _actions.Clear();
        foreach (var (definition, pref) in ActionCatalog.Ordered(config))
        {
            _actions.Add(new() { Id = definition.Id, Title = definition.Title + (definition.Toolbar ? " · 顶部工具栏" : ""), Shortcut = definition.Shortcut, Enabled = pref.Enabled, IsPrimary = definition.Toolbar || pref.IsPrimary, CanPin = !definition.Toolbar, Group = pref.Group, Rule = pref.Rule.Clone() });
        }
        _custom.Clear(); foreach (var custom in config.CustomActions) _custom.Add(custom);
        _workspace.Load(config, () => _actions.Where(x => x.CanPin).Select(x => (x.Id, x.Title)).Concat(_custom.Select(x => (x.Id, x.Title))).ToArray(), _app.LastContext);
        _shellEdits.Clear();
        foreach (var key in config.PinnedShellActions.Concat(config.HiddenShellActions).Concat(_app.Config.PinnedShellActions).Concat(_app.Config.HiddenShellActions).Distinct(StringComparer.Ordinal))
            _shellEdits[key] = new() { Key = key, Pinned = config.PinnedShellActions.Contains(key), Hidden = config.HiddenShellActions.Contains(key) };
        RefreshShellItems();
        RefreshConfigurationSafety();
        RefreshDiagnostics(); SaveStatus.Text = "修改后点击“保存设置”生效。";
    }
    public void RefreshDiagnostics()
    {
        InputStatus.Text = _app.InputStatus;
        var ctx = _app.LastContext;
        DiagnosticsText.Text = ctx == null ? "还没有右键记录。请先在文件、文件夹或桌面上打开一次 BetterCM。\n\n配置文件：" + _app.ConfigPath : $"最近一次右键：{ctx.CapturedAt:HH:mm:ss}\n识别来源：{ctx.Source}\n选中项目：{ctx.Items.Count} 个\n所在位置：{ctx.CurrentDirectory}\n目标路径：{string.Join("\n", ctx.TargetPaths)}\n\n{_app.Timing}\n配置文件：{_app.ConfigPath}";
        DiagnosticsText.Text += "\n\n" + _app.MenuDiagnostics;
        if (_app.RecentMenuRequests.LastOrDefault() is { } request)
            DiagnosticsText.Text += $"\n\n最近输入请求 #{request.RequestId}（{request.Input}）：{request.Outcome}\n累计耗时：排队 {request.QueueMs:F1} / 上下文 {request.ContextReadyMs:F1} / Shell {request.ShellReadyMs:F1} / 动作 {request.ActionsReadyMs:F1} / 显示 {request.WindowShownMs:F1} / 呈现观测 {request.PresentationObservedMs:F1} ms\n键盘焦点：{(request.KeyboardReady ? "已就绪" : "未确认")}（呈现观测不含动画完成）";
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
    private void ResetMotion_Click(object sender, RoutedEventArgs e) { LoadMotion(new()); SaveStatus.Text = "已重置动画选项，点击“保存设置”后生效。"; }
    internal AppConfig ReadSettings()
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
        config.Actions = _actions.Select((x, i) => new ActionPreference { Id = x.Id, Enabled = x.Enabled, IsPrimary = x.IsPrimary, Order = i, Group = x.Group, Rule = x.Rule.Clone() }).ToList();
        config.CustomActions = _custom.ToList();
        _workspace.Apply(config);
        foreach (var row in _shellEdits.Values) { if (row.Pinned) config.PinnedShellActions.Add(row.Key); else config.PinnedShellActions.Remove(row.Key); if (row.Hidden) config.HiddenShellActions.Add(row.Key); else config.HiddenShellActions.Remove(row.Key); }
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
            SaveStatus.Text = error ?? "设置已保存，下次打开菜单时使用新设置。";
        }
        catch (Exception ex) { SaveStatus.Text = ex.Message; }
        RefreshDiagnostics();
    }
    public void NotifyConfigurationChanged(AppConfig? previousInternalConfig = null)
    {
        if (previousInternalConfig != null && !_configurationConflict && _loadedConfig == ConfigJson.Snapshot(previousInternalConfig))
        {
            bool startup = StartWithWindows.IsChecked == true;
            var pending = SettingsRebase.Merge(_loadedSettings, ReadSettings(), _app.Config);
            LoadSettings(pending, startup);
            SaveStatus.Text = "已同步应用内的设置变更，未保存的编辑仍然保留。";
            return;
        }
        RefreshDiagnostics();
        _configurationConflict = _loadedConfig != ConfigJson.Snapshot(_app.Config);
        if (_configurationConflict)
            SaveStatus.Text = "配置已在外部更新。你的编辑仍然保留；请点击“撤销未保存的修改”加载最新设置，再继续编辑。";
        RefreshConfigurationSafety();
    }
    private void RefreshConfigurationSafety()
    {
        SaveButton.IsEnabled = !_configurationConflict && _validHotkey && _app.ConfigurationError == null;
        ConfigurationNotice.Visibility = _app.ConfigurationError == null ? Visibility.Collapsed : Visibility.Visible;
        ConfigurationError.Text = _app.ConfigurationError ?? "";
        RecoverConfigurationButton.IsEnabled = _app.HasConfigurationBackup;
    }
    private void RetryConfiguration_Click(object sender, RoutedEventArgs e)
    {
        if (_app.ReloadConfiguration()) RefreshSettings();
        else RefreshConfigurationSafety();
    }
    private void RecoverConfiguration_Click(object sender, RoutedEventArgs e)
    {
        var error = _app.RestoreConfigurationBackup();
        if (error == null) RefreshSettings();
        RefreshConfigurationSafety();
        SaveStatus.Text = error ?? "已恢复上一份配置。损坏的原文件已另外保留。";
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
        RefreshCustomSelection();
    }
    private void UseEnhanced_Click(object sender, RoutedEventArgs e)
    {
        ClassicOption.IsChecked = false;
        SaveStatus.Text = "已选择增强模式。可以继续调整，保存设置后生效。";
    }
    private void SettingsTab_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source == SettingsTabs && ShellGrid != null && SettingsTabs.SelectedItem is TabItem { Header: "系统项目" }) RefreshShellItems();
    }
    private void RefreshShell_Click(object sender, RoutedEventArgs e) => RefreshShellItems();
    private void RefreshShellItems() => RefreshShellItems(_app.LastShellItems, _app.LastContext?.PrimaryName ?? "");
    internal void RefreshShellItems(IReadOnlyList<ShellItem> items, string targetName)
    {
        _shell.Clear();
        foreach (var item in items.DistinctBy(x => x.Key))
        {
            if (!_shellEdits.TryGetValue(item.Key, out var row))
            {
                row = new() { Key = item.Key, Title = item.Title, Group = item.Group, Pinned = _app.Config.PinnedShellActions.Contains(item.Key), Hidden = _app.Config.HiddenShellActions.Contains(item.Key) };
                _shellEdits.Add(item.Key, row);
            }
            row.Title = item.Title; row.Group = item.Group;
            _shell.Add(row);
        }
        ShellContextText.Text = _shell.Count == 0 ? "尚未读取右键项目" : $"最近一次右键{(targetName.Length > 0 ? " · " + targetName : "")} · {_shell.Count} 项";
    }
    private void CustomSelection_Changed(object sender, SelectionChangedEventArgs e)
        => RefreshCustomSelection();
    private void RefreshCustomSelection()
    {
        if (EditCustomButton == null || RemoveCustomButton == null || CustomCommands == null) return;
        bool selected = CustomCommands.IsEnabled && CustomGrid.SelectedItem is CustomActionItem;
        int index = selected && CustomGrid.SelectedItem is CustomActionItem item ? _custom.IndexOf(item) : -1;
        EditCustomButton.IsEnabled = RemoveCustomButton.IsEnabled = selected;
        if (MoveCustomUpButton != null) MoveCustomUpButton.IsEnabled = selected && index > 0;
        if (MoveCustomDownButton != null) MoveCustomDownButton.IsEnabled = selected && index >= 0 && index < _custom.Count - 1;
    }
    private void Hotkey_Changed(object sender, TextChangedEventArgs e)
    {
        if (HotkeyValidation == null || SaveButton == null) return;
        string? error = null;
        try { Hotkey.Parse(HotkeyText.Text.Trim()); } catch (ArgumentException ex) { error = ex.Message; }
        _validHotkey = error == null;
        HotkeyValidation.Text = error ?? "格式可用。保存时会检查是否与其他程序冲突。";
        HotkeyValidation.SetResourceReference(TextBlock.ForegroundProperty, error == null ? "MutedBrush" : "ErrorBrush");
        HotkeyText.SetResourceReference(Control.BorderBrushProperty, error == null ? "BorderBrush" : "ErrorBrush");
        RefreshConfigurationSafety();
    }
    private void RestoreSaved_Click(object sender, RoutedEventArgs e)
    {
        foreach (var grid in new[] { BuiltinGrid, CustomGrid, ShellGrid }) { grid.CancelEdit(DataGridEditingUnit.Cell); grid.CancelEdit(DataGridEditingUnit.Row); }
        _app.CancelPreview(); RefreshSettings(); SaveStatus.Text = "未保存的修改已撤销，当前显示已保存的设置。";
    }
    private void MoveUp_Click(object sender, RoutedEventArgs e) => Move(-1);
    private void EditBuiltinRule_Click(object sender, RoutedEventArgs e)
    {
        if (BuiltinGrid.SelectedItem is not ActionRow row) { SaveStatus.Text = "请先选择一个内置动作。"; return; }
        var dialog = new ActionRuleWindow(row.Title, row.Rule, row.Group, _app.LastContext, row.CanPin) { Owner = this };
        if (dialog.ShowDialog() == true) { row.Rule = dialog.Result; row.Group = dialog.Group; BuiltinGrid.Items.Refresh(); SaveStatus.Text = "场景与分组已更新，保存设置后生效。"; }
    }
    private void MoveDown_Click(object sender, RoutedEventArgs e) => Move(1);
    private void Move(int direction) { if (BuiltinGrid.SelectedItem is ActionRow row) { int current = _actions.IndexOf(row), target = current + direction; if (target >= 0 && target < _actions.Count) { _actions.Move(current, target); BuiltinGrid.SelectedItem = row; } } }
    private void MoveCustomUp_Click(object sender, RoutedEventArgs e) => MoveCustom(-1);
    private void MoveCustomDown_Click(object sender, RoutedEventArgs e) => MoveCustom(1);
    private void MoveCustom(int direction) { if (CustomGrid.SelectedItem is CustomActionItem item) { int current = _custom.IndexOf(item), target = current + direction; if (target >= 0 && target < _custom.Count) { _custom.Move(current, target); CustomGrid.SelectedItem = item; RefreshCustomSelection(); } } }
    private void AddCustom_Click(object sender, RoutedEventArgs e) { var dialog = new CustomActionWindow(new(), _app.LastContext) { Owner = this }; if (dialog.ShowDialog() == true) { _custom.Add(dialog.Result); RefreshCustomSelection(); } }
    private void EditCustom_Click(object sender, RoutedEventArgs e) { if (CustomGrid.SelectedItem is CustomActionItem item) { var dialog = new CustomActionWindow(item, _app.LastContext) { Owner = this }; if (dialog.ShowDialog() == true) { _custom[_custom.IndexOf(item)] = dialog.Result; RefreshCustomSelection(); } } }
    private async void ImportCustom_Click(object sender, RoutedEventArgs e)
    {
        if (_customTransferBusy) return;
        var picker = new Microsoft.Win32.OpenFileDialog { Filter = "BetterCM 自定义动作 (*.json)|*.json", CheckFileExists = true, Title = "导入自定义动作" };
        if (picker.ShowDialog(this) != true) return;
        SetTransferBusy(true);
        try
        {
            var incoming = await Task.Run(() => CustomActionTransfer.ReadAsync(picker.FileName));
            if (!IsVisible || _app.IsExiting) return;
            var dialog = new CustomActionImportWindow(_custom, incoming) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.Result is { } result) ApplyCustomImport(result);
        }
        catch (Exception ex) { AppLog.Write("custom-import", ex); SaveStatus.Text = "导入失败，当前列表未改动：" + ex.Message; }
        finally { SetTransferBusy(false); }
    }
    internal void ApplyCustomImport(CustomActionMerge result)
    {
        _custom.Clear(); foreach (var action in result.Actions) _custom.Add(action);
        RefreshCustomSelection(); SaveStatus.Text = $"已加入待保存列表：新增 {result.Added}、替换 {result.Replaced}、跳过 {result.Skipped}。点击“保存设置”生效。";
    }
    private async void ExportCustom_Click(object sender, RoutedEventArgs e)
    {
        if (_customTransferBusy) return;
        var picker = new Microsoft.Win32.SaveFileDialog { Filter = "BetterCM 自定义动作 (*.json)|*.json", FileName = "BetterCM-CustomActions.json", OverwritePrompt = true, Title = "导出当前动作列表" };
        if (picker.ShowDialog(this) != true) return;
        SetTransferBusy(true);
        try
        {
            var actions = new AppConfig { CustomActions = _custom.ToList() }.Clone().CustomActions;
            await Task.Run(() => CustomActionTransfer.WriteAsync(picker.FileName, actions));
            SaveStatus.Text = $"已导出 {actions.Count} 个动作（包含程序路径与参数），未保存的列表修改也已包含。";
        }
        catch (Exception ex) { AppLog.Write("custom-export", ex); SaveStatus.Text = "导出失败：" + ex.Message; }
        finally { SetTransferBusy(false); }
    }
    private void SetTransferBusy(bool busy)
    { _customTransferBusy = busy; ImportCustomButton.IsEnabled = ExportCustomButton.IsEnabled = !busy; }
    private void RemoveCustom_Click(object sender, RoutedEventArgs e) { if (CustomGrid.SelectedItem is CustomActionItem item) { _custom.Remove(item); RefreshCustomSelection(); } }
    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        try { _app.PreviewWithOptions(ReadSettings()); SaveStatus.Text = "正在预览当前选项，关闭菜单后恢复已保存的外观"; }
        catch (Exception ex) { SaveStatus.Text = ex.Message; }
    }
    private void OpenConfig_Click(object sender, RoutedEventArgs e) => OpenLocation(_app.ConfigPath);
    private void OpenLocation(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { AppLog.Write("settings-open", ex); SaveStatus.Text = "无法打开：" + ex.Message; }
    }
    private async void ExportDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "ZIP 诊断包 (*.zip)|*.zip", FileName = "BetterCM-diagnostics-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip", OverwritePrompt = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var snapshot = _app.Config.Clone();
            double dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this).PixelsPerInchX;
            // Export atomically, preserving an existing destination until the archive is complete.
            string temporary = dialog.FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var requests = _app.RecentMenuRequests;
            try { await Task.Run(() => DiagnosticExporter.Export(temporary, snapshot, dpi, menuRequests: requests)); File.Move(temporary, dialog.FileName, true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            SaveStatus.Text = "诊断包已导出（路径、文件名及自定义内容已省略，日志仅含摘要），分享前请先检查。";
        }
        catch (Exception ex) { AppLog.Write("diagnostics-export", ex); SaveStatus.Text = "导出失败：" + ex.Message; }
    }
    private void OpenLogs_Click(object sender, RoutedEventArgs e) => OpenLocation(ConfigStore.DataDirectory);
    private void Hide_Click(object sender, RoutedEventArgs e) => Hide();
    private void Window_Closing(object? sender, CancelEventArgs e) { if (!_app.IsExiting) { e.Cancel = true; Hide(); } }
}
