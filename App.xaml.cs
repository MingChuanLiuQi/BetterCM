using System.IO;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Services;
using BetterCM.Views;
using Forms = System.Windows.Forms;
using Microsoft.Win32;
namespace BetterCM;

public partial class App : Application, IMenuHost
{
    private static string InstanceName => AppInstance.Name(Environment.GetEnvironmentVariable("BETTERCM_DATA_DIR"));
    private Mutex? _installationPresence;
    private Mutex? _mutex;
    private EventWaitHandle? _wake, _exitSignal;
    private RegisteredWaitHandle? _wakeWait, _exitWait;
    private Forms.NotifyIcon? _tray;
    private readonly ConfigStore _store = new();
    private readonly ExplorerContextService _contexts = new();
    private readonly ShellBridge _shell = new();
    private ActionExecutor? _executor;
    private InputService? _input;
    private MainWindow? _settings;
    private ContextMenuWindow? _menu;
    private ConfigurationWatcher? _watcher;
    private bool _executing;
    private CancellationTokenSource? _captureCancellation, _shellCancellation;
    private int _triggerGeneration;
    private int _menuRequest;
    private AppearancePreview? _appearance;
    private CancellationTokenSource? _detailsCancellation;
#if QA
    private bool _previewInactive;
#else
    private const bool _previewInactive = false;
#endif
    private string? _lastInputFailure;
    public bool IsExiting { get; private set; }
    public AppConfig Config => _store.Current;
    public string ConfigPath => _store.FilePath;
    public string InputStatus => (_interceptionPaused ? "异常保护：已暂停右键接管。可从托盘恢复，或勾选“使用 BetterCM”后保存设置。\n" : "") + (_input?.Status ?? "输入服务未启动");
    public SelectedContext? LastContext { get; private set; }
    public IReadOnlyList<ShellItem> LastShellItems { get; private set; } = [];
    private IReadOnlyList<ShellMenuNode> _lastNativeMenu = [];
    public string Timing { get; private set; } = "";
    public string MenuDiagnostics => _menu?.Diagnostics ?? "菜单正在初始化";
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _helperMode = AppInstance.IsHelper(e.Args);
        DispatcherUnhandledException += DispatcherFailure;
        TaskScheduler.UnobservedTaskException += UnobservedFailure;
        AppDomain.CurrentDomain.UnhandledException += DomainFailure;
        try
        {
#if QA
            if (_helperMode && e.Args.Length < 2) throw new ArgumentException("辅助宿主缺少管道参数。");
            if (await TryRunDiagnosticsAsync(e.Args)) return;
#else
            if (!StartupRouter.IsSupported(e.Args)) { AppLog.Write("arguments", new ArgumentException("不支持的启动参数")); Shutdown(2); return; }
#endif
            if (e.Args.Length >= 2 && e.Args[0] == "--shell-host") { await ShellHost.RunAsync(e.Args[1], Dispatcher); Shutdown(); return; }
            if (e.Args.Length >= 2 && e.Args[0] == "--context-host") { await ExplorerContextService.RunHostAsync(e.Args[1], Dispatcher); Shutdown(); return; }
            if (e.Args.Contains("--shutdown")) { Shutdown(await AppInstance.StopAsync(InstanceName, TimeSpan.FromSeconds(10)) ? 0 : 1); return; }
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) { MessageBox.Show(AppInfo.DisplayName + " 需要 Windows 11。"); Shutdown(); return; }
            _mutex = new Mutex(true, @"Local\" + InstanceName + ".App", out bool first);
            if (!first)
            {
                await AppInstance.WakeAsync(InstanceName);
                Shutdown(); return;
            }
            // Presence marker is shared by all data profiles, so setup can detect isolated instances too.
            _installationPresence = new Mutex(false, AppInstance.InstallationMutex);
            _wake = new(false, EventResetMode.AutoReset, @"Local\" + InstanceName + ".Wake");
            _exitSignal = new(false, EventResetMode.AutoReset, @"Local\" + InstanceName + ".Exit");
            _wakeWait = ThreadPool.RegisterWaitForSingleObject(_wake, (_, _) => Dispatcher.BeginInvoke(ShowSettings), null, -1, false);
            _exitWait = ThreadPool.RegisterWaitForSingleObject(_exitSignal, (_, _) => Dispatcher.BeginInvoke(ExitApplication), null, -1, false);
            _store.Load(); ThemeService.Apply(Config.Theme, Config.Material);
            _appearance = new(() => Config, config => ThemeService.Apply(config.Theme, config.Material));
            _input = CreateInput();
            _input.Apply(Config.GlobalHotkey, Config.ReplaceExplorerMenu);
            _executor = new(_shell);
            _menu = new() { Host = this };
            // Inspection changes taskbar discoverability only; rendering and all dismissal paths stay enabled.
#if QA
            _menu.ShowInTaskbar = e.Args.Contains("--inspect-menu");
            _previewInactive = e.Args.Contains("--preview-inactive");
#endif
            _menu.ObservationChanged += observation => _input?.WatchMenu(observation);
            _menu.IsVisibleChanged += (_, _) => { if (!_menu.IsVisible) { _appearance.Restore(); _detailsCancellation?.Cancel(); } };
            SystemEvents.UserPreferenceChanged += WindowsPreferenceChanged;
            _menu.ObservationChanged += _ => { if (_settings?.IsVisible == true) _settings.RefreshDiagnostics(); if (!_menu.IsExpanding) ObserveQa(); };
            CreateTray(); ReportInputStatus(); WatchConfiguration();
            _ = Task.WhenAll(_contexts.PrepareAsync(), _shell.PrepareAsync());
            _ = Dispatcher.BeginInvoke(() => { if (!IsExiting) _menu.PreparePresentation(); }, DispatcherPriority.ContextIdle);
            if (!e.Args.Contains("--background")) ShowSettings();
            if (_store.LastError != null) Notify(_store.LastError, true);
#if QA
            var previewIndex = Array.IndexOf(e.Args, "--preview-file");
            if (previewIndex >= 0 && e.Args.Length > previewIndex + 1 && (File.Exists(e.Args[previewIndex + 1]) || Directory.Exists(e.Args[previewIndex + 1])))
            {
                var path = Path.GetFullPath(e.Args[previewIndex + 1]); bool directory = Directory.Exists(path);
                LastContext = new() { Source = "菜单预览", CurrentDirectory = directory ? path : Path.GetDirectoryName(path)!, Items = [new(path, directory)] };
                _ = Dispatcher.BeginInvoke(async () =>
                {
                    NativeMethods.GetCursorPos(out var point);
                    await ShowMenuAsync(point.X, point.Y, LastContext!, Config);
                    if (e.Args.Contains("--preview-expanded")) _menu.ExpandMore();
                }, DispatcherPriority.ApplicationIdle);
            }
#endif
        }
        catch (Exception ex) { AppLog.Write("startup", ex); if (!_helperMode) MessageBox.Show(ex.Message, "BetterCM 启动失败"); Shutdown(1); }
    }
    private void CreateTray()
    {
        using var icon = Assembly.GetExecutingAssembly().GetManifestResourceStream("BetterCM.Assets.BetterCM.ico");
        _tray = new Forms.NotifyIcon { Icon = icon == null ? System.Drawing.SystemIcons.Application : new System.Drawing.Icon(icon), Text = AppInfo.DisplayName + " · 右键菜单", Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("设置与一级菜单管理", null, (_, _) => Dispatcher.Invoke(ShowSettings));
        menu.Items.Add("预览菜单", null, (_, _) => Dispatcher.Invoke(Preview));
        menu.Items.Add("暂停 / 恢复右键接管", null, (_, _) => Dispatcher.Invoke(() => { var next = Config.Clone(); next.ReplaceExplorerMenu = _interceptionPaused || !next.ReplaceExplorerMenu; var error = SaveSettings(next, StartupService.Enabled); if (error != null) Notify(error, true); else Notify(_input?.InterceptionActive == true ? "已恢复右键接管" : next.ReplaceExplorerMenu ? "右键接管未能恢复，请查看设置中的输入状态。" : "已暂停右键接管"); }));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出 BetterCM", null, (_, _) => Dispatcher.Invoke(ExitApplication));
        _tray.ContextMenuStrip = menu;
        _tray.MouseClick += (_, args) => { if (args.Button == Forms.MouseButtons.Left) Dispatcher.Invoke(ShowSettings); };
    }
    public void ShowSettings()
    {
        if (IsExiting) return;
        bool created = _settings == null;
        if (created) { _settings = new(this); MainWindow = _settings; }
        _menuRequest++; _shellCancellation?.Cancel();
        _input?.WatchPreparation(null);
        _menu?.CloseMenu("打开设置");
        _appearance?.Restore();
        if (!_settings!.IsVisible && !created) _settings.PrepareForShow();
        _settings.RefreshModeAvailability();
        _settings.Show();
        if (_settings.WindowState == WindowState.Minimized) _settings.WindowState = WindowState.Normal;
        _settings.Activate();
    }
    public void Preview()
        => PreviewWithOptions(Config);
    public void PreviewWithOptions(AppConfig options)
    {
        if (_menu == null) return;
        ConfigStore.Validate(options);
        var context = LastContext ?? new SelectedContext { Source = "菜单预览 · 桌面文件夹", CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) };
        NativeMethods.GetCursorPos(out var point); _ = PreviewMenuAsync(point.X, point.Y, context, options);
    }
    private async Task PreviewMenuAsync(int x, int y, SelectedContext context, AppConfig options)
    {
        try { await ShowMenuAsync(x, y, context, options); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AppLog.Write("preview", ex); ReportError("菜单预览失败：" + ex.Message); }
    }
    public void CancelPreview()
    {
        _menuRequest++; _shellCancellation?.Cancel();
        _input?.WatchPreparation(null);
        _menu?.CloseMenu("还原已保存设置", true);
        _appearance?.Restore(); ThemeService.Apply(Config.Theme, Config.Material);
    }
    private async Task TriggerAsync(TriggerRequest request)
    {
        if (IsExiting) return;
        _captureCancellation?.Cancel();
        var cancellation = _captureCancellation = new CancellationTokenSource();
        int trigger = ++_triggerGeneration;
        _menuRequest++; _shellCancellation?.Cancel();
        _input?.WatchPreparation(null);
        _menu?.CloseMenu("新的右键请求", true);
        _appearance?.Restore();
        var timer = Stopwatch.StartNew();
        try
        {
            var clipboard = ExplorerContextService.UsesExplorerContext(NativeMethods.ClassName(request.Window)) ? [] : ReadClipboardPaths();
            var context = await _contexts.CaptureAsync(request.Window, clipboard, request.PointerActivation ? new NativeMethods.POINT { X = request.X, Y = request.Y } : null, cancellation.Token, request.NavigationActivation);
            if (trigger != _triggerGeneration || cancellation.IsCancellationRequested || IsExiting) return;
            LastContext = context; Timing = $"上下文准备：{timer.Elapsed.TotalMilliseconds:F0} ms";
            await ShowMenuAsync(request.X, request.Y, context); if (_settings?.IsVisible == true) _settings.RefreshDiagnostics();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (trigger != _triggerGeneration || IsExiting) return;
            AppLog.Write("trigger", ex);
            try { await ShowMenuAsync(request.X, request.Y, new() { SourceWindow = request.Window, Source = "上下文识别失败，请用 Shift+右键" }); }
            catch (Exception fallback) { AppLog.Write("trigger-fallback", fallback); ReportError("菜单暂时无法显示，请按住 Shift 再右键使用 Windows 菜单。"); }
        }
        finally { if (_captureCancellation == cancellation) _captureCancellation = null; cancellation.Dispose(); }
    }
    private static IReadOnlyList<string> ReadClipboardPaths()
    {
        try
        {
            if (Clipboard.ContainsFileDropList()) return Clipboard.GetFileDropList().Cast<string>().ToArray();
            if (Clipboard.ContainsText()) return Clipboard.GetText().Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim('"', '\'')).Where(Path.IsPathFullyQualified).ToArray();
        }
        catch (Exception ex) { AppLog.Write("clipboard-read", ex); }
        return [];
    }
    private async Task ShowMenuAsync(int x, int y, SelectedContext context, AppConfig? options = null)
    {
        if (_menu == null) return;
        var config = (options ?? Config).Clone();
        var toolbar = new List<MenuAction>(); var primary = new List<MenuAction>(); var more = new List<MenuAction>();
        Task<ShellResponse>? shellLoad = null;
        int request = ++_menuRequest;
        _menu.CloseMenu("准备新菜单", true);
        _appearance?.Begin(request, config, options != null);
        bool presented = false;
        CancellationToken cancellation = default;
        try
        {
            _shellCancellation?.Cancel(); _shellCancellation?.Dispose();
            _shellCancellation = new();
            cancellation = _shellCancellation.Token;
            LastShellItems = [];
            _lastNativeMenu = [];
            LastContext = context;
            void BuildEnhancedActions()
            {
                foreach (var (definition, pref) in ActionCatalog.Ordered(config))
                {
                    if (!pref.Enabled || !definition.Supports(context)) continue;
                    var action = new MenuAction { Id = definition.Id, Title = definition.Title, Glyph = definition.Glyph, Shortcut = definition.Shortcut, Enabled = !ShellCommandMap.IsNative(definition.Id), Execute = () => RunActionAsync(definition.Id, context, shellLoad) };
                    (definition.Toolbar ? toolbar : pref.IsPrimary ? primary : more).Add(action);
                }
            }
            async Task<(MenuAction Action, bool Primary)[]> LoadCustomActionsAsync()
                => await Task.WhenAll(config.CustomActions.Where(a => ActionCatalog.Matches(a, context)).Select(async custom => (await CustomActionPresentation.CreateAsync(custom, () => RunCustomAsync(custom, context), cancellation), custom.IsPrimary)));
            var customLoad = config.ClassicMenu ? null : LoadCustomActionsAsync();
            if (!config.ClassicMenu) BuildEnhancedActions();
            // Read native capabilities before the first frame. Replacing disabled placeholders
            // after Show() used to shrink the normal menu and animate an unintended second expansion.
            ShellResponse? response = null;
            var timer = Stopwatch.StartNew();
            if (context.HasTarget)
            {
                var owner = options != null ? NativeMethods.GetForegroundWindow() : context.SourceWindow;
                // While Shell builds the menu, a new pointer gesture or foreground switch
                // cancels this request instead of allowing a late popup to steal focus.
                _input?.WatchPreparation(new(owner, default, -request, (uint)Environment.TickCount));
                try { shellLoad = _shell.OpenAsync(context, cancellation, deferSubmenus: config.ClassicMenu); response = await shellLoad; }
                finally { if (request == _menuRequest) _input?.WatchPreparation(null); }
            }
            if (cancellation.IsCancellationRequested || request != _menuRequest || IsExiting) return;
            bool nativeLayout = config.ClassicMenu && response?.Success == true;
            if (config.ClassicMenu && !nativeLayout)
            {
                // Fall back for this invocation only; keep the user's saved mode.
                config.ClassicMenu = false; config.ShowMoreItems = false;
                BuildEnhancedActions();
            }
            if (!nativeLayout)
            {
                foreach (var custom in await (customLoad ?? LoadCustomActionsAsync())) (custom.Primary ? primary : more).Add(custom.Action);
                cancellation.ThrowIfCancellationRequested();
                if (request != _menuRequest || IsExiting) return;
            }
            List<MenuAction>? shellActions = null;
            if (response != null)
            {
                LastShellItems = response.Success ? response.Items ?? [] : [];
                _lastNativeMenu = response.Success ? response.Menu ?? [] : [];
                shellActions = CreateShellActions(response, config);
                Timing += $"；系统项目准备：{timer.Elapsed.TotalMilliseconds:F0} ms";
            }
            int generation = _menu.ShowAt(new MenuPresentationRequest
            {
                X = x,
                Y = y,
                Context = context,
                Config = config,
                Toolbar = toolbar,
                Primary = primary,
                More = more,
                Activate = !_previewInactive,
                Shell = shellActions,
                ShellError = response?.Success == false ? response.Error + "\n已回退到增强菜单，可使用内置路径及自定义动作；Shift+右键可打开 Windows 菜单。" : !context.HasTarget ? context.Source : null,
                NativeMenu = nativeLayout ? response?.Menu ?? [] : null,
                NativeRefresh =
                config.ClassicMenu && context.ShellBackground && context.SourceWindow != 0 ? () => RunAsync(async (_, _) => { var refreshed = await _shell.RefreshAsync(context); if (!refreshed.Success) throw new InvalidOperationException(refreshed.Error); return null; }) : null,
                NativeLoad = config.ClassicMenu && response?.Session is { } session ? async submenu =>
                {
                    var page = await _shell.ExpandAsync(session, submenu);
                    if (request != _menuRequest || page.Superseded) throw new OperationCanceledException("菜单上下文已改变");
                    if (!page.Success) throw new InvalidOperationException(page.Error);
                    if (submenu == null) LastShellItems = page.Items ?? [];
                    _lastNativeMenu = submenu is int token ? NativeMenuTree.Replace(_lastNativeMenu, token, page.Menu ?? []) : page.Menu ?? [];
                    return new NativeMenuPage(page.Menu ?? [], CreateShellActions(page, config));
                }
                : null
            });
            presented = true;
            _ = FillDetailsAsync(generation, context);
            ObserveQa();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally { if (!presented) _appearance?.Restore(request); }
    }
    private async Task FillDetailsAsync(int generation, SelectedContext ctx)
    {
        _detailsCancellation?.Cancel();
        var cancellation = _detailsCancellation = new CancellationTokenSource();
        try
        {
            await MenuDetails.LoadAsync(MenuDetails.IconAsync(ctx.PrimaryPath, ctx.IsDirectory, cancellation: cancellation.Token), MenuDetails.SizeAsync(ctx.PrimaryPath, ctx.IsSingleItem && !ctx.IsDirectory, cancellation: cancellation.Token),
                (icon, size) => _menu?.SetDetails(generation, icon, ctx.Source.Split('·')[0].Trim() + (size is long length ? " · " + FormatSize(length) : "")), cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally { if (_detailsCancellation == cancellation) _detailsCancellation = null; cancellation.Dispose(); }
    }
    private List<MenuAction> CreateShellActions(ShellResponse response, AppConfig config)
    {
        if (!response.Success) return [];
        return (response.Items ?? []).Where(a => config.ClassicMenu || !config.HiddenShellActions.Contains(a.Key)).Select(item => new MenuAction
        {
            Id = config.ClassicMenu ? "native:" + item.Command : "shell:" + item.Command + ":" + item.Key,
            ShellKey = item.Key,
            Title = item.Title,
            Group = item.Group,
            Verb = item.Verb,
            Detail = item.Group.Length == 0 ? item.Title : item.Group + " · " + item.Title,
            Glyph = "\uE8A7",
            Shortcut = item.Shortcut,
            Enabled = item.Enabled,
            Execute = () => RunShellAsync(response.Session!, item.Command)
        }).ToList();
    }
    [System.Diagnostics.Conditional("QA")]
    private void ObserveQa()
    {
#if QA
        if (Environment.GetEnvironmentVariable("BETTERCM_QA_REPORT") is not { Length: > 0 } report) return;
        if (_menu == null || !double.IsFinite(_menu.Height) || !double.IsFinite(_menu.MaxHeight)) return;
        try
        {
            File.WriteAllText(report, System.Text.Json.JsonSerializer.Serialize(new { Version = AppInfo.Version, Time = DateTimeOffset.Now, AcceptedClicks = _input?.AcceptedClicks, NativePopups = _input?.NativePopups, Context = LastContext, ShellItems = LastShellItems, NativeMenu = _lastNativeMenu, Timing, Menu = MenuDiagnostics, Layout = _menu?.LayoutDiagnostics, LayoutState = _menu?.LayoutState, Cascade = _menu?.CascadeLayout }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { AppLog.Write("qa-observation", ex); }
#endif
    }
    private async Task RunActionAsync(string id, SelectedContext ctx, Task<ShellResponse>? shellLoad = null) => await RunAsync(async (token, progress) => await _executor!.ExecuteAsync(id, ctx, token, progress, ShellCommandMap.IsNative(id) && shellLoad != null ? await shellLoad : null), id == "hash", !ShellCommandMap.IsNative(id), "正在计算 SHA256");
    private async Task RunCustomAsync(CustomActionItem action, SelectedContext ctx) => await RunAsync(async (token, progress) => { await _executor!.ExecuteCustomAsync(action, ctx, token, progress); return null; }, showProgress: action.SelectionMode == "each", operationTitle: "正在启动自定义动作", operationSubtitle: action.Title);
    private async Task RunShellAsync(string session, int command) => await RunAsync(async (_, _) => { var result = await _shell.InvokeAsync(session, command); if (!result.Success) throw new InvalidOperationException(result.Error); return null; }, exclusive: false);
    private async Task RunAsync(Func<CancellationToken, IProgress<(double, string)>, Task<string?>> action, bool showProgress = false, bool exclusive = true, string operationTitle = "正在执行操作", string? operationSubtitle = null)
    {
        if (exclusive && _executing) { Notify("已有操作正在执行，请等待或取消"); return; }
        if (exclusive) _executing = true;
        using var cancellation = new CancellationTokenSource();
        OperationWindow? operation = showProgress ? new(cancellation, operationTitle, operationSubtitle) : null;
        var progress = new Progress<(double, string)>(value => operation?.Report(value.Item1, value.Item2));
        try { operation?.Show(); var message = await action(cancellation.Token, progress); if (message != null) Notify(message); }
        catch (OperationCanceledException) { Notify("操作已取消"); }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { Notify("管理员授权已取消"); }
        catch (Exception ex) { AppLog.Write("action", ex); Notify(ex.Message, true); }
        finally { operation?.Close(); if (exclusive) _executing = false; }
    }
    public void ToggleShellPin(string key)
    {
        var previous = Config.Clone();
        var next = Config.Clone(); if (!next.PinnedShellActions.Add(key)) next.PinnedShellActions.Remove(key);
        if (!_store.Save(next)) Notify(_store.LastError!, true);
        else _settings?.NotifyConfigurationChanged(previous);
    }
    public string? SaveSettings(AppConfig next, bool startup)
    {
        try
        {
            var previous = Config.Clone();
            var error = SettingsPersistence.Save(_store, next, startup, StartupService.Capture, StartupService.SetEnabled, StartupService.Restore);
            if (error != null) return error;
            ApplySavedInput(); ThemeService.Apply(Config.Theme, Config.Material);
            _settings?.NotifyConfigurationChanged(previous);
            return null;
        }
        catch (Exception ex) { AppLog.Write("settings", ex); return ex.Message; }
    }
    private InputService CreateInput()
    {
        var input = new InputService();
        input.Triggered += request => _ = TriggerAsync(request);
        input.MenuDismissRequested += (generation, reason) =>
        {
            if (generation < 0)
            {
                if (generation == -_menuRequest) { _menuRequest++; _shellCancellation?.Cancel(); _input?.WatchPreparation(null); _appearance?.Restore(); }
            }
            else _menu?.Dismiss(generation, reason);
        };
        return input;
    }
    private void ApplyInput() { _input?.Apply(Config.GlobalHotkey, Config.ReplaceExplorerMenu && !_interceptionPaused); ReportInputStatus(); _settings?.RefreshDiagnostics(); }
    internal void ApplySavedInput()
    {
        if (Config.ReplaceExplorerMenu)
        {
            if (_interceptionPaused && _input == null) _input = CreateInput();
            _interceptionPaused = false;
        }
        ApplyInput();
    }
    private void ReportInputStatus()
    {
        if (_input == null || _input.PrimaryHotkeyAvailable) { _lastInputFailure = null; return; }
        if (_lastInputFailure == _input.Status) return;
        _lastInputFailure = _input.Status;
        Notify("主快捷键注册失败。" + _input.Status, true);
    }
    private static string FormatSize(long size) => size < 1024 ? $"{size} B" : size < 1048576 ? $"{size / 1024.0:F1} KB" : size < 1073741824 ? $"{size / 1048576.0:F1} MB" : $"{size / 1073741824.0:F2} GB";
    private void WatchConfiguration()
    {
        try { _watcher = new(ConfigPath, Dispatcher, () => { try { if (_store.HasDiskChanges && !ReloadConfiguration()) ReportError(_store.LastError!); } catch (Exception ex) { AppLog.Write("config-reload", ex); ReportError(ex.Message); } }); }
        catch (Exception ex) { AppLog.Write("config-watch", ex); }
    }

    private void WindowsPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (!IsExiting) Dispatcher.BeginInvoke(() =>
        {
            if (IsExiting) return;
            try { ThemeService.Apply(Config.Theme, Config.Material); }
            catch (Exception ex) { AppLog.Write("system-theme", ex); }
        });
    }
    public void Notify(string text, bool error = false)
    {
        if (error || Config.ShowNotifications) _tray?.ShowBalloonTip(4000, error ? "BetterCM · 操作未完成" : "BetterCM", text, error ? Forms.ToolTipIcon.Warning : Forms.ToolTipIcon.Info);
    }
    public void ExitApplication() { if (IsExiting) return; IsExiting = true; Shutdown(); }
    protected override void OnExit(ExitEventArgs e)
    {
        IsExiting = true; _captureCancellation?.Cancel(); _shellCancellation?.Cancel(); _watcher?.Dispose(); _input?.Dispose(); _shell.Dispose(); _contexts.Dispose();
        SystemEvents.UserPreferenceChanged -= WindowsPreferenceChanged;
        DispatcherUnhandledException -= DispatcherFailure; TaskScheduler.UnobservedTaskException -= UnobservedFailure;
        AppDomain.CurrentDomain.UnhandledException -= DomainFailure;
        _wakeWait?.Unregister(null); _exitWait?.Unregister(null); _wake?.Dispose(); _exitSignal?.Dispose();
        _installationPresence?.Dispose();
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        if (_mutex != null) { try { _mutex.ReleaseMutex(); } catch (ApplicationException) { } _mutex.Dispose(); }
        base.OnExit(e);
    }
}
