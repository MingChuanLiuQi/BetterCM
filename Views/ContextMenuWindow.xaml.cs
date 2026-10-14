using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Services;
namespace BetterCM.Views;

public partial class ContextMenuWindow : Window
{
    private List<MenuAction> _primary = [], _more = [], _shell = [], _toolbar = [];
    private bool _expanded;
    private int _x, _y, _generation;
    private uint _shownAt;
    private readonly HoverExpansion _moreHover = new();
    private readonly SurfaceAnimator _animator;
    private readonly MenuFirstFrame _firstFrame = new();
    internal bool PresentationPending => _firstFrame.Pending;
    internal nint ContentHandle => _contentSource?.Handle ?? 0;
    internal double FirstFrameDelayMs => _firstFrame.ReadyDelayMs;
    private bool _closing;
    private bool _opening, _activationSucceeded;
    private HwndSource? _contentSource;
    private readonly ExpansionAnimator _expansion = new();
    private readonly HashSet<string> _revealIds = [];
    private readonly Dictionary<ListBoxItem, string> _revealingRows = [];
    private string _revealStyle = "none";
    private bool _preparingExpansion;
    private string _appliedSearch = "", _expansionCancelReason = "";
    private SearchPlaceholder? _searchHint;
    public bool SearchHintVisible => _searchHint?.Visibility == Visibility.Visible;
    public bool IsExpanding => _expansion.Active;
    internal bool ExpansionFrameUpdating => _expansion.UpdatingFrame;
    public bool ContentCoversWindow
    {
        get
        {
            if (_contentSource == null) return false;
            NativeMethods.GetWindowRect(new WindowInteropHelper(this).Handle, out var parent);
            NativeMethods.GetWindowRect(_contentSource.Handle, out var child);
            return child.Right - child.Left >= parent.Right - parent.Left && child.Bottom - child.Top >= parent.Bottom - parent.Top;
        }
    }
    public string LayoutDiagnostics { get; private set; } = "";
    public object LayoutState
    {
        get
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle == 0 || _contentSource == null) return new { Created = false };
            NativeMethods.GetWindowRect(handle, out var bounds);
            NativeMethods.GetWindowRect(_contentSource?.Handle ?? 0, out var contentBounds);
            return new { Height, ActualHeight, MaxHeight, IsExpanding, CancelReason = _expansionCancelReason, Scale = _scale, NativeWidth = bounds.Right - bounds.Left, NativeHeight = bounds.Bottom - bounds.Top, WorkHeight = _work.Bottom - _work.Top,
                ParentTransform = HwndSource.FromHwnd(handle)?.CompositionTarget?.TransformToDevice.M22, ChildTransform = _contentSource?.CompositionTarget?.TransformToDevice.M22,
                ContentNativeHeight = contentBounds.Bottom - contentBounds.Top, SurfaceHeight = Surface.Height, SurfaceActual = Surface.ActualHeight, ListHeight = ActionsList.ActualHeight, FooterBottom = SettingsButton.PointToScreen(new Point(0, SettingsButton.ActualHeight)).Y, Bottom = bounds.Bottom };
        }
    }
    private NativeMethods.RECT _work;
    private double _scale = 1;
    private AppConfig _config = new();
    private readonly NativeMenuState _nativeState = new();
    private IReadOnlyList<ShellMenuNode>? _nativeMenu { get => _nativeState.Menu; set => _nativeState.SetMenu(value); }
    private Func<Task>? _nativeRefresh;
    private Func<int?, Task<NativeMenuPage>>? _nativeLoad;
    private Task<NativeMenuPage>? _loadingAll;
    private readonly Dictionary<(double Scale, double Width, bool Compact, bool Separator, bool Group, bool Pin), double> _rowHeights = [];
    public event Action<MenuObservation?>? ObservationChanged;
    public int VisibleGeneration => _generation;
    internal int NativeTransitionResult { get; set; } = unchecked((int)0x80004005);
    public bool IsOpen => IsVisible && !_closing;
    public bool HasTransparentContentWindow => _contentSource != null && ReferenceEquals(_contentSource.RootVisual, Surface) && NativeMethods.GetParent(_contentSource.Handle) == new WindowInteropHelper(this).Handle && (NativeMethods.GetWindowLongPtr(_contentSource.Handle, NativeMethods.GWL_EXSTYLE).ToInt64() & NativeMethods.WS_EX_LAYERED) != 0;
    private string _dismissReason = "尚未显示";
    public string Diagnostics => $"菜单：{(IsOpen ? "显示" : _closing && IsVisible ? "正在关闭" : "隐藏")}；关闭原因：{_dismissReason}\n窗口：{new WindowInteropHelper(this).Handle}；代次：{_generation}；行数：{ActionsList.Items.Count}；高度：{ActualHeight:F0} DIP\n焦点：{(_activationSucceeded ? "已激活" : "未激活")}；独立材质：{ThemeService.HasStableMenuBackdrop(this)}；扩展样式：{NativeMethods.GetWindowLongPtr(new WindowInteropHelper(this).Handle, NativeMethods.GWL_EXSTYLE).ToInt64():X}\n{ThemeService.Diagnostics}";
    public ContextMenuWindow()
    {
        InitializeComponent(); ThemeService.Attach(this); _animator = new(Surface);
        Search.Loaded += (_, _) =>
        {
            if (_searchHint != null || AdornerLayer.GetAdornerLayer(Search) is not { } layer) return;
            _searchHint = new(Search); layer.Add(_searchHint);
        };
        Activated += (_, _) => { _activationSucceeded = true; Dispatcher.BeginInvoke(FocusContent, System.Windows.Threading.DispatcherPriority.Input); };
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            // This empty native parent owns the backdrop, never text input. SendInput and
            // foreground activation can briefly focus it before FocusContent runs. Remove
            // its default IME association; leave the separate WPF search HWND untouched.
            NativeMethods.ImmAssociateContext(handle, 0);
            long style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GWL_STYLE).ToInt64();
            NativeMethods.SetWindowLongPtr(handle, NativeMethods.GWL_STYLE, (nint)(style & ~NativeMethods.WS_SYSMENU)); // WS_SYSMENU: a menu has no caption buttons.
            NativeMethods.SetWindowPos(handle, 0, 0, 0, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);
            // Desktop composition owns the parent backdrop; a child HWND presents WPF above it.
            // Keeping the two renderers on one HWND lets the native backdrop cover WPF's redirection surface.
            Content = null; SizeToContent = SizeToContent.Manual;
            Surface.Resources = Resources;
            Surface.SetResourceReference(TextElement.ForegroundProperty, "TextBrush");
            Surface.PreviewKeyDown += Window_KeyDown;
            _contentSource = new(new HwndSourceParameters("BetterCM menu content")
            {
                ParentWindow = handle,
                WindowStyle = NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE | NativeMethods.WS_CLIPSIBLINGS | NativeMethods.WS_CLIPCHILDREN,
                UsesPerPixelTransparency = true,
                Width = 320, Height = 320
            });
            _contentSource.CompositionTarget.BackgroundColor = Colors.Transparent;
            _contentSource.RootVisual = Surface;
            HwndSource.FromHwnd(handle)?.AddHook((nint hwnd, int message, nint wp, nint lp, ref bool handled) =>
            {
                // The parent owns DWM but the child owns WPF keyboard input. Activating the
                // empty parent (including keyboard accessibility) must restore child focus.
                if (message == NativeMethods.WM_SETFOCUS) Dispatcher.BeginInvoke(FocusContent, System.Windows.Threading.DispatcherPriority.Input);
                return 0;
            });
        };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { _firstFrame.Cancel(); CancelExpansion(); _moreHover.Cancel(); _animator.Reset(); if (_parentMenu != null && _contentSource != null) _contentSource.RootVisual = null; ObservationChanged?.Invoke(null); } };
        Closed += (_, _) => { _windowClosed = true; _firstFrame.Cancel(); DisposeSubmenus(); CancelExpansion(); _moreHover.Cancel(); _animator.Reset(); _contentSource?.Dispose(); };
        ActionsList.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, e) => { if (e.VerticalChange != 0) CloseSubmenu(); }));
        SizeChanged += (_, _) => PublishBounds();
    }
    public int ShowAt(int x, int y, SelectedContext context, AppConfig config, List<MenuAction> toolbar, List<MenuAction> primary, List<MenuAction> more, bool activate = true, List<MenuAction>? shell = null, string? shellError = null, IReadOnlyList<ShellMenuNode>? nativeMenu = null, Func<Task>? nativeRefresh = null, Func<int?, Task<NativeMenuPage>>? nativeLoad = null)
    {
        ObservationChanged?.Invoke(null);
        if (IsVisible) Hide();
        CloseSubmenu();
        CancelExpansion();
        _opening = true; _activationSucceeded = false;
        _moreHover.Cancel(); _animator.Reset(); _closing = false; IsHitTestVisible = Surface.IsHitTestVisible = true;
        _generation++; _shownAt = (uint)Environment.TickCount; _x = x; _y = y; _config = config; _dismissReason = "本次尚未关闭";
        _nativeState.Reset(nativeMenu, []);
        _nativeRefresh = nativeRefresh;
        _nativeLoad = nativeLoad; _loadingAll = null; _loadingGroups.Clear(); _activeGroupLoad = null;
        _toolbar = toolbar; _primary = primary; _more = more; _shell = []; _expanded = nativeMenu != null || !config.ShowMoreItems || config.CompactMenu;
        ApplyMetrics(config);
        TargetDetail.Visibility = config.CompactMenu ? Visibility.Collapsed : Visibility.Visible;
        _appliedSearch = ""; Search.Text = ""; Search.Visibility = _expanded && !config.CompactMenu ? Visibility.Visible : Visibility.Collapsed; More.Visibility = _expanded ? Visibility.Collapsed : Visibility.Visible;
        ShellStatus.Text = shellError ?? "";
        ShellStatus.Visibility = _expanded && ShellStatus.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        TargetName.Text = context.Items.Count > 1 ? $"已选中 {context.Items.Count} 个项目" : context.HasTarget ? context.PrimaryName : "未识别到文件或文件夹";
        TargetDetail.Text = context.Source.Split('·')[0].Trim(); TargetName.ToolTip = context.PrimaryPath;
        TargetDetail.Visibility = config.ShowFileDetails && !config.CompactMenu ? Visibility.Visible : Visibility.Collapsed;
        TargetIcon.Source = null;
        Toolbar.ItemsSource = toolbar; Toolbar.Visibility = toolbar.Count > 0 && !config.CompactMenu ? Visibility.Visible : Visibility.Collapsed;
        if (shell != null) ApplyShell(shell, shellError);
        Rebuild();
        var handle = new WindowInteropHelper(this).EnsureHandle();
        if (_contentSource != null && _contentSource.RootVisual == null) _contentSource.RootVisual = Surface;
        NativeMethods.SetWindowPos(handle, 0, x, y, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        var monitor = NativeMethods.MonitorFromPoint(new() { X = x, Y = y }, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        NativeMethods.GetMonitorInfo(monitor, ref info); _work = info.rcWork;
        _scale = Math.Max(1, NativeMethods.GetDpiForWindow(handle) / 96.0);
        MaxHeight = Math.Max(1, Math.Min(640, (_work.Bottom - _work.Top - 12) / _scale));
        Surface.MaxHeight = MaxHeight;
        ShellStatus.MaxHeight = MaxHeight / 3;
        Height = MeasureContent();
        ResizeNative(Height);
        ShowActivated = activate;
        // Prepare the visual state and final HWND/content size before the first visible frame.
        bool held = SurfaceAnimator.OpeningAnimated(config) && _firstFrame.Hold(handle);
        if (held) _animator.PrepareOpening(config); else _animator.Play(config, true);
        ShowActivated = !held && activate;
        Show(); UpdateLayout();
        _shownAt = (uint)Environment.TickCount; _opening = false;
        ActionsList.SelectedIndex = -1; PublishBounds();
        int shownGeneration = _generation;
        void BeginPresentation()
        {
            if (_generation != shownGeneration || !IsOpen) return;
            if (held) _animator.Play(config, true);
            ActivateContent(activate, context.SourceWindow, handle);
            PublishBounds();
        }
        if (held) _firstFrame.Present(Dispatcher, BeginPresentation); else BeginPresentation();
        return _generation;
    }
    private void ActivateContent(bool activate, nint sourceWindow, nint handle)
    {
        if (!activate) return;
        NativeMethods.SetForegroundWindow(handle);
        _activationSucceeded = Activate() && NativeMethods.GetForegroundWindow() == handle;
        if (_activationSucceeded) FocusContent();
        if (!_activationSucceeded)
        {
            int generation = _generation;
            Dispatcher.BeginInvoke(() =>
            {
                var foreground = NativeMethods.GetForegroundWindow();
                if (_generation != generation || !IsOpen || !MenuFocusPolicy.CanRetry(sourceWindow, handle, foreground)) return;
                NativeMethods.SetForegroundWindow(handle);
                _activationSucceeded = Activate() && NativeMethods.GetForegroundWindow() == handle;
                if (_activationSucceeded) FocusContent();
            }, System.Windows.Threading.DispatcherPriority.Input);
        }
    }
    private void ApplyMetrics(AppConfig config)
    {
        var metrics = MenuMetrics.For(config.CompactMenu);
        Width = metrics.Width; Resources["ActionPadding"] = metrics.ActionPadding; Resources["ActionFontSize"] = metrics.FontSize;
    }
    internal void PreparePresentation() => new WindowInteropHelper(this).EnsureHandle();
    private void FocusContent()
    {
        if (!IsOpen || _contentSource == null || NativeMethods.GetForegroundWindow() != new WindowInteropHelper(this).Handle) return;
        NativeMethods.SetFocus(_contentSource.Handle);
        if (!Surface.IsKeyboardFocusWithin) ActionsList.Focus();
    }
    public void CloseMenu(string reason = "关闭菜单", bool immediate = false)
    {
        if (!IsVisible) return;
        if (_firstFrame.Pending) immediate = true;
        CloseSubmenu();
        CancelExpansion();
        if (_closing) { if (immediate) { _animator.Reset(); Hide(); _closing = false; IsHitTestVisible = true; } return; }
        _closing = true; _dismissReason = reason; _moreHover.Cancel(); IsHitTestVisible = Surface.IsHitTestVisible = false;
        ObservationChanged?.Invoke(null);
        int generation = _generation;
        void Finish() { if (_generation != generation || !_closing) return; Hide(); _animator.Reset(); _closing = false; IsHitTestVisible = true; }
        if (immediate) { _animator.Reset(); Finish(); } else _animator.Play(_config, false, Finish);
    }
    public void Dismiss(int generation, MenuDismissReason reason) { if (MenuDismissPolicy.IsCurrent(_generation, IsOpen, generation)) CloseMenu(reason == MenuDismissReason.Pointer ? "外部鼠标输入" : "前台窗口切换或移动"); }
    public void SetDetails(int generation, ImageSource? icon, string details)
    {
        if (_generation != generation || !IsOpen) return;
        TargetIcon.Source = icon; TargetDetail.Text = details;
    }
    public void SetShell(int generation, List<MenuAction> actions, string? error)
    {
        if (_generation != generation || !IsOpen) return;
        CloseSubmenu();
        ApplyShell(actions, error);
        // Loading system capabilities is not a user expansion gesture.
        // Production menus already receive these entries in ShowAt, before Show().
        Reflow("none", 0, () => Rebuild());
    }
    private void ApplyShell(List<MenuAction> actions, string? error)
    {
        if (_nativeMenu != null)
        {
            _toolbar = []; _primary = []; _more = []; _shell = []; _nativeState.ReplaceCommands(actions);
            Toolbar.ItemsSource = null; Toolbar.Visibility = Visibility.Collapsed;
            ShellStatus.Text = error ?? ""; ShellStatus.Visibility = error != null ? Visibility.Visible : Visibility.Collapsed;
            return;
        }
        if (error == null)
        {
            List<MenuAction> Bind(List<MenuAction> entries) => entries.Select(x => ShellCommandMap.Bind(x, actions)).OfType<MenuAction>().ToList();
            _toolbar = Bind(_toolbar); _primary = Bind(_primary); _more = Bind(_more);
            Toolbar.ItemsSource = _toolbar;
            Toolbar.Visibility = _toolbar.Count > 0 && !_config.CompactMenu ? Visibility.Visible : Visibility.Collapsed;
        }
        var mapped = _primary.Concat(_more).Concat(_toolbar).Where(x => x.ShellKey != null).Select(x => x.ShellKey).ToHashSet();
        _shell = _config.IncludeShellActions ? actions.Where(a => !mapped.Contains(a.ShellKey) && !MenuComposer.DuplicatesBuiltin(a, _primary.Concat(_more).Concat(_toolbar))).ToList() : [];
        ShellStatus.Text = error ?? "";
        ShellStatus.Visibility = _expanded && error != null ? Visibility.Visible : Visibility.Collapsed;
    }
    private void Rebuild(bool preserveScroll = false)
    {
        if (ActionsList == null) return;
        var selected = (ActionsList.SelectedItem as MenuEntry)?.Id;
        var scroll = FindScroll(ActionsList);
        var offset = scroll?.VerticalOffset ?? 0;
        var filter = _config.CompactMenu ? "" : Search?.Text ?? "";
        ActionsList.ItemsSource = _nativeMenu != null ? _nativeState.Rows(filter) : MenuComposer.Compose(_config.CompactMenu ? _toolbar.Concat(_primary).ToList() : _primary, _more, _shell, _config.PinnedShellActions, _expanded, filter, !_config.ShowMoreItems || _config.CompactMenu);
        ActionsList.SelectedItem = ActionsList.Items.Cast<MenuEntry>().FirstOrDefault(x => x.Id == selected && selected != "");
        if (preserveScroll && scroll != null) { ActionsList.UpdateLayout(); scroll.ScrollToVerticalOffset(offset); }
    }
    private static ScrollViewer? FindScroll(DependencyObject root)
    {
        if (root is ScrollViewer viewer) return viewer;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindScroll(VisualTreeHelper.GetChild(root, i)) is { } child) return child;
        return null;
    }
    private void Expand()
    {
        _moreHover.Cancel();
        if (_expanded || !IsOpen) return;
        Reflow(_config.MoreExpansionAnimation, _config.MoreExpansionMs, () =>
        {
            _expanded = true; More.Visibility = Visibility.Collapsed; Search.Visibility = Visibility.Visible;
            ShellStatus.Visibility = ShellStatus.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            Rebuild();
        });
    }
    public void ExpandMore() => Expand();
    internal bool FocusSearch()
    {
        if (!IsOpen || _parentMenu != null || _config.CompactMenu) return false;
        Expand();
        return Search.IsVisible && Search.Focus();
    }
    private void CancelExpansion(string reason = "布局或生命周期")
    {
        _expansionCancelReason = reason;
        _expansion.Cancel(); _revealIds.Clear(); _revealingRows.Clear(); _preparingExpansion = false;
        if (ActionsList == null) return;
        for (int i = 0; i < ActionsList.Items.Count; i++)
            if (ActionsList.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem row) ResetRow(row);
    }
    private static void ResetRow(ListBoxItem row)
    {
        row.BeginAnimation(OpacityProperty, null); row.Opacity = 1;
        if (row.RenderTransform is TranslateTransform translation) translation.BeginAnimation(TranslateTransform.YProperty, null);
        row.RenderTransform = Transform.Identity;
    }
    private void Reflow(string style, int duration, Action update)
    {
        var handle = new WindowInteropHelper(this).Handle;
        double previousHeight = NativeMethods.GetWindowRect(handle, out var bounds) ? (bounds.Bottom - bounds.Top) / _scale : Height;
        var previousIds = ActionsList.Items.Cast<MenuEntry>().Select(x => x.Id).ToHashSet();
        CancelExpansion();
        update();
        if (!IsOpen || !ExpansionAnimator.Enabled(_config, style)) { Reposition(); return; }
        _revealStyle = style;
        foreach (var row in ActionsList.Items.Cast<MenuEntry>()) if (!previousIds.Contains(row.Id)) _revealIds.Add(row.Id);
        _preparingExpansion = true;
        double targetHeight;
        try
        {
            targetHeight = MeasureContent();
            for (int i = 0; i < ActionsList.Items.Count; i++)
                if (ActionsList.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem row) PrepareRow(row);
        }
        finally { _preparingExpansion = false; }
        // Keep the layered WPF bitmap at its final size. Resizing it every frame queues
        // stale UpdateLayeredWindow sizes. The nonlayered parent clips/reveals this bitmap.
        ResizeContent(targetHeight);
        _expansion.Play(previousHeight, targetHeight, duration, _config.AnimationEasing, height =>
        {
            int revision = _expansion.Revision;
            ResizeNative(height, false);
            if (revision != _expansion.Revision || !IsOpen) return;
            foreach (var pair in _revealingRows)
                if (pair.Key.DataContext is MenuEntry entry && entry.Id == pair.Value) RevealRow(pair.Key);
        }, () =>
        {
            // The final frame already has the final geometry and row values. A reset here
            // invalidates the layered bitmap at exactly the visible animation endpoint.
            _expansionCancelReason = "动画完成"; _revealIds.Clear(); _revealingRows.Clear(); PublishBounds();
        }, () =>
        {
            for (int i = 0; i < ActionsList.Items.Count; i++)
                if (ActionsList.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem row) AnimateRow(row);
        });
    }
    private void MenuRow_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ListBoxItem row) return;
        row.DataContextChanged -= MenuRow_DataContextChanged;
        row.DataContextChanged += MenuRow_DataContextChanged;
        if (_preparingExpansion || _expansion.Preparing) PrepareRow(row); else AnimateRow(row);
    }
    private void MenuRow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not ListBoxItem row) return;
        _revealingRows.Remove(row);
        ResetRow(row);
        if (_preparingExpansion || _expansion.Preparing) PrepareRow(row); else AnimateRow(row);
    }
    private void PrepareRow(ListBoxItem row)
    {
        if (row.DataContext is not MenuEntry entry || !_revealIds.Contains(entry.Id)) return;
        if (_revealStyle.Contains("fade", StringComparison.Ordinal)) row.Opacity = 0;
        if (_revealStyle.Contains("slide", StringComparison.Ordinal)) row.RenderTransform = new TranslateTransform(0, -6);
    }
    private void AnimateRow(ListBoxItem row)
    {
        if (!_expansion.Active || row.DataContext is not MenuEntry entry || !_revealIds.Remove(entry.Id)) return;
        _revealingRows[row] = entry.Id;
        RevealRow(row);
    }
    private void RevealRow(ListBoxItem row)
    {
        // One monotonic clock drives size, fade and translation. Independent WPF clocks
        // can resynchronize after the layered-window resize and briefly rewind a row.
        double progress = _expansion.Progress;
        if (_revealStyle.Contains("fade", StringComparison.Ordinal)) row.Opacity = progress;
        if (_revealStyle.Contains("slide", StringComparison.Ordinal))
        {
            if (row.RenderTransform is not TranslateTransform) row.RenderTransform = new TranslateTransform();
            ((TranslateTransform)row.RenderTransform).Y = -6 * (1 - progress);
        }
    }
    private void Reposition()
    {
        if (!IsOpen) return;
        double height = MeasureContent();
        ResizeNative(height);
    }
    private void ResizeNative(double heightDip, bool resizeContent = true)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _scale = Math.Max(1, NativeMethods.GetDpiForWindow(handle) / 96.0);
        // The detached content source lays out synchronously; the parent Window's ActualHeight
        // updates later. Use the measured dimensions so expansion cannot restore the old size.
        int width = (int)Math.Ceiling(Width * _scale), height = (int)Math.Ceiling(heightDip * _scale);
        int x = Math.Clamp(_x, _work.Left + 4, Math.Max(_work.Left + 4, _work.Right - width - 4));
        int y = Math.Clamp(_y, _work.Top + 4, Math.Max(_work.Top + 4, _work.Bottom - height - 4));
        bool hasBounds = NativeMethods.GetWindowRect(handle, out var current);
        bool moved = !hasBounds || current.Left != x || current.Top != y;
        if (moved || current.Right - current.Left != width || current.Bottom - current.Top != height)
            NativeMethods.SetWindowPos(handle, 0, x, y, width, height, NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        if (resizeContent) ResizeContent(heightDip);
        SyncContentPosition(moved);
        PublishBounds();
    }
    private void SyncContentPosition(bool parentMoved = false)
    {
        if (_contentSource == null) return;
        var handle = new WindowInteropHelper(this).Handle;
        // A layered child can retain its screen position when its backdrop parent moves
        // upwards at the work-area edge. Align it explicitly without resizing its bitmap.
        var origin = new NativeMethods.POINT(); NativeMethods.ClientToScreen(handle, ref origin);
        bool drifted = NativeMethods.GetWindowRect(_contentSource.Handle, out var child) && (child.Left != origin.X || child.Top != origin.Y);
        if (drifted)
            NativeMethods.SetWindowPos(_contentSource.Handle, 0, 0, 0, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        if (parentMoved || drifted)
        {
            // HwndTarget's layered OnResize refreshes screen coordinates and completes
            // the render synchronously. Notify the unchanged client size after moving its
            // parent; a plain move notification can still leave an older ULW in flight.
            NativeMethods.GetWindowRect(_contentSource.Handle, out child);
            var size = MenuWindowSize.Pack(child.Right - child.Left, child.Bottom - child.Top);
            NativeMethods.SendRenderSizeMessage(_contentSource.Handle, NativeMethods.WM_SIZE, 0, size);
        }
    }
    private void ResizeContent(double heightDip)
    {
        if (_contentSource == null) return;
        int width = (int)Math.Ceiling(Width * _scale), height = (int)Math.Ceiling(heightDip * _scale);
        if (!NativeMethods.GetWindowRect(_contentSource.Handle, out var current) || current.Right - current.Left != width || current.Bottom - current.Top != height)
            NativeMethods.SetWindowPos(_contentSource.Handle, 0, 0, 0, width, height, NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        SyncContentPosition();
    }
    private double MeasureContent()
    {
        if (_contentSource == null) return Height;
        Surface.Height = double.NaN;
        Surface.InvalidateMeasure();
        Surface.MaxHeight = MaxHeight;
        Surface.Measure(new Size(Width, MaxHeight));
        // A virtualized ListBox reports its current viewport as DesiredSize. Measure one
        // template per row kind, then let the Grid assign the bounded viewport. This keeps
        // a partially expanded viewport from becoming the next expansion's final height.
        var grid = (Grid)Surface.Child;
        double chrome = Surface.Padding.Top + Surface.Padding.Bottom + Surface.BorderThickness.Top + Surface.BorderThickness.Bottom;
        foreach (FrameworkElement child in grid.Children)
            if (child != ActionsList && child.Visibility != Visibility.Collapsed)
            {
                child.Measure(new Size(Math.Max(1, Width - 12), double.PositiveInfinity));
                chrome += child.DesiredSize.Height;
            }
        double rowsHeight = 0;
        var measuredKinds = new List<string>();
        static bool Multiline(MenuEntry row) => new[] { row.Title, row.Hint, row.Glyph, row.Source }.Any(text => text.IndexOfAny(['\r', '\n', '\u2028', '\u2029']) >= 0);
        double MeasureRow(MenuEntry entry)
        {
            var prototype = new ListBoxItem { Content = entry, ContentTemplate = ActionsList.ItemTemplate, Resources = Surface.Resources, Style = (Style)Surface.FindResource(typeof(ListBoxItem)), Visibility = Visibility.Hidden };
            grid.Children.Add(prototype);
            try
            {
                prototype.Measure(new Size(Math.Max(1, Width - Surface.Padding.Left - Surface.Padding.Right), double.PositiveInfinity));
                return Math.Ceiling(prototype.DesiredSize.Height * _scale) / _scale;
            }
            finally { grid.Children.Remove(prototype); }
        }
        foreach (var kind in ActionsList.Items.Cast<MenuEntry>().GroupBy(x => (x.IsSeparator, x.IsGroup, x.CanPin)))
        {
            var metricKey = (_scale, Width, _config.CompactMenu, kind.Key.IsSeparator, kind.Key.IsGroup, kind.Key.CanPin);
            var ordinary = kind.Where(row => !Multiline(row)).ToArray();
            if (ordinary.Length > 0)
            {
                if (!_rowHeights.TryGetValue(metricKey, out double cached))
                {
                    // Single-line templates have fixed density; explicit line breaks
                    // are measured individually and never poison this shared cache.
                    cached = MeasureRow(ordinary[0]);
                    if (_rowHeights.Count >= 24) _rowHeights.Clear();
                    _rowHeights[metricKey] = cached;
                }
                rowsHeight += cached * ordinary.Length;
                measuredKinds.Add($"{kind.Key}:{cached:F1}x{ordinary.Length}");
            }
            foreach (var exceptional in kind.Where(Multiline))
            {
                double measured = MeasureRow(exceptional); rowsHeight += measured;
                measuredKinds.Add($"{kind.Key}:multiline={measured:F1}");
            }
        }
        // WPF applies the native minimum tracking height again when a cached HWND is
        // shown. Account for it before presentation, so a one-row compact submenu
        // cannot grow past its transparent content bitmap after Show.
        double minimumPixels = NativeMethods.GetSystemMetricsForDpi(NativeMethods.SM_CYMINTRACK, (uint)Math.Round(_scale * 96));
        double height = Math.Min(MaxHeight, Math.Max(minimumPixels, Math.Ceiling((chrome + rowsHeight + ActionsList.Padding.Top + ActionsList.Padding.Bottom) * _scale)) / _scale);
        LayoutDiagnostics = $"chrome={chrome:F1}, rows={rowsHeight:F1}, height={height:F1}, max={MaxHeight:F1}; " + string.Join("; ", measuredKinds);
        Surface.Height = height;
        Surface.Measure(new Size(Width, height)); Surface.Arrange(new Rect(0, 0, Width, height));
        return height;
    }
    private void PublishBounds()
    {
        if (_parentMenu != null) { RootMenu.PublishBounds(); return; }
        if (!IsOpen || _opening) return;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != 0 && NativeMethods.GetWindowRect(handle, out var bounds)) ObservationChanged?.Invoke(new(handle, bounds, _generation, _shownAt, SubmenuRegions()));
    }
    private async void Action_Click(object sender, RoutedEventArgs e)
    {
        if (!IsOpen || sender is not Button button) return;
        if (button.Tag is MenuEntry { IsGroup: true } group) { OpenGroup(group); return; }
        var action = button.Tag is MenuEntry entry ? entry.Action : button.Tag as MenuAction;
        if (action is { Enabled: true }) { RootMenu.CloseMenu("执行动作", true); if (action.Execute != null) await action.Execute(); }
    }
    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: MenuEntry { Action.ShellKey: { } key } })
        {
            ((App)Application.Current).ToggleShellPin(key); RootMenu._config = ((App)Application.Current).Config;
            RootMenu.CloseSubmenu(); RootMenu.Rebuild(); RootMenu.Reposition();
        }
    }
    private void More_Enter(object sender, MouseEventArgs e)
    {
        if (!IsOpen || _expanded) return;
        int generation = _generation;
        _moreHover.Schedule(_config.MoreHoverDelayMs, () => { if (_generation == generation && IsOpen && More.IsMouseOver) Expand(); });
    }
    private void More_Leave(object sender, MouseEventArgs e) => _moreHover.Cancel();
    private void More_Click(object sender, RoutedEventArgs e) => Expand();
    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        if (_appliedSearch == Search.Text) return;
        _appliedSearch = Search.Text; CloseSubmenu(); CancelExpansion("搜索变化");
        if (Search.Text.Length > 0 && _nativeLoad != null && _nativeMenu != null && NativeMenuTree.HasDeferred(_nativeMenu))
        {
            Rebuild(); if (IsVisible) Reposition();
            if (_loadingAll == null) _ = CompleteSearchAsync();
            return;
        }
        Rebuild(); if (IsVisible) Reposition();
    }
    private async Task CompleteSearchAsync()
    {
        int generation = _generation;
        try
        {
            var page = await (_loadingAll = _nativeLoad!(null));
            if (_generation != generation || !IsOpen) return;
            if (Search.Text.Length == 0)
            {
                // The user cleared search and returned to root browsing. Hydrate the
                // completed tree in place without replacing root row containers or closing an open submenu.
                _nativeState.AddCommands(page.Actions);
                _nativeState.SetMenu(page.Menu);
                var hydrated = _nativeState.Rows();
                var current = VisibleRows;
                for (int i = 0; i < Math.Min(current.Count, hydrated.Count); i++)
                    if (current[i].IsGroup && current[i].DeferredSubmenu != null)
                    {
                        current[i].Children = hydrated[i].Children;
                        current[i].DeferredSubmenu = null;
                    }
                return;
            }
            _nativeState.Reset(page.Menu, page.Actions); Rebuild(); Reposition(); RootMenu.PublishBounds();
        }
        catch (OperationCanceledException) { /* Superseded search completion leaves the visible menu alone. */ }
        catch (Exception ex) { if (_generation == generation && IsOpen && Search.Text.Length > 0) ShowLoadError(ex); }
        finally { if (_generation == generation) _loadingAll = null; }
    }
    private void ShowLoadError(Exception exception)
    {
        AppLog.Write("shell-expand", exception);
        ShellStatus.Text = "子菜单读取失败，请重试或使用 Shift+右键"; ShellStatus.Visibility = Visibility.Visible; Reposition();
    }
    private void Settings_Click(object sender, RoutedEventArgs e) { CloseMenu("打开设置"); ((App)Application.Current).ShowSettings(); }
    private void Window_Deactivated(object sender, EventArgs e)
    {
        _activationSucceeded = false;
        if (!IsOpen || _opening) return;
        int generation = RootMenu._generation;
        Dispatcher.BeginInvoke(() =>
        {
            if (RootMenu._generation == generation && RootMenu.IsOpen && !RootMenu.ContainsMenuHandle(NativeMethods.GetForegroundWindow())) RootMenu.CloseMenu("窗口失焦");
        }, System.Windows.Threading.DispatcherPriority.Input);
    }
    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.ImeProcessed && !Search.IsKeyboardFocusWithin) key = e.ImeProcessedKey;
        var modifiers = Keyboard.Modifiers;
        if (Environment.GetEnvironmentVariable("BETTERCM_QA_REPORT") is { Length: > 0 } report)
            try { File.AppendAllText(report + ".keys.jsonl", System.Text.Json.JsonSerializer.Serialize(new { Time = DateTimeOffset.Now, Key = key.ToString(), Original = e.Key.ToString(), Modifiers = modifiers.ToString(), SearchFocus = Search.IsKeyboardFocusWithin }) + "\n"); } catch { }
        if (!IsOpen) { e.Handled = true; return; }
        if (key == Key.Escape) { if (_parentMenu != null) ReturnToParent(); else CloseMenu("Escape"); e.Handled = true; return; }
        if (key == Key.Left && modifiers == ModifierKeys.None && _parentMenu != null) { ReturnToParent(); e.Handled = true; return; }
        if (key == Key.F10 && modifiers == ModifierKeys.Shift) { Expand(); e.Handled = true; return; }
        if (key == Key.F && modifiers == ModifierKeys.Control) { FocusSearch(); e.Handled = true; return; }
        if (Search.IsKeyboardFocusWithin && key == Key.Down && modifiers == ModifierKeys.None)
        {
            ActionsList.Focus(); ActionsList.SelectedItem = VisibleRows.FirstOrDefault(x => x.Enabled && !x.IsSeparator); e.Handled = true; return;
        }
        if (Search.IsKeyboardFocusWithin && !(key == Key.Enter || modifiers == ModifierKeys.Alt)) return;
        var selected = ActionsList.SelectedItem as MenuEntry;
        if (RootMenu._nativeMenu != null && modifiers is ModifierKeys.None or ModifierKeys.Alt && key >= Key.A && key <= Key.Z)
        {
            string mnemonic = ((char)('A' + key - Key.A)).ToString();
            var matches = VisibleRows.Where(x => x.Enabled && x.Mnemonic == mnemonic).ToList();
            if (matches.Count > 0)
            {
                e.Handled = true;
                if (matches.Count > 1) { ActionsList.Focus(); ActionsList.SelectedItem = matches[(matches.FindIndex(x => x == selected) + 1) % matches.Count]; return; }
                var match = matches[0];
                if (match.IsGroup) OpenGroup(match, true);
                else if (match.Action?.Execute is { } execute) { RootMenu.CloseMenu("原生访问键执行", true); await execute(); }
                return;
            }
        }
        if (RootMenu._nativeMenu != null && key == Key.F5 && modifiers == ModifierKeys.None && RootMenu._nativeRefresh is { } refresh)
        { RootMenu.CloseMenu("原生视图刷新", true); e.Handled = true; await refresh(); return; }
        if (modifiers == ModifierKeys.None && selected is { IsGroup: true } && key is Key.Right or Key.Left or Key.Enter)
        {
            if (key != Key.Left) OpenGroup(selected, focus: true);
            e.Handled = true; return;
        }
        if (key == Key.Right && modifiers == ModifierKeys.None) { Expand(); e.Handled = true; return; }
        string? id = (key, modifiers) switch
        {
            (Key.F5, ModifierKeys.None) => "refresh",
            (Key.C, ModifierKeys.Control) => "copy", (Key.X, ModifierKeys.Control) => "cut",
            (Key.V, ModifierKeys.Control) => "paste", (Key.F2, ModifierKeys.None) => "rename", (Key.Delete, ModifierKeys.None) => "delete",
            (Key.Enter, ModifierKeys.Alt) => "properties",
            (Key.T, ModifierKeys.None) => "terminal", (Key.C, ModifierKeys.None) => "code",
            (Key.N, ModifierKeys.None) => "notepad", (Key.P, ModifierKeys.None) => "path",
            (Key.U, ModifierKeys.None) => "slash", (Key.H, ModifierKeys.None) => "hash", _ => null
        };
        var action = RootMenu._nativeMenu != null
            ? MenuKeyboard.NativeAction(RootMenu._nativeState.Rows(), key, modifiers)
            : RootMenu._primary.Concat(RootMenu._more).Concat(RootMenu._toolbar).FirstOrDefault(x => x.Id == id && x.Enabled);
        if (key == Key.Enter && modifiers == ModifierKeys.None) action = MenuKeyboard.EnterAction(VisibleRows, selected, RootMenu._nativeMenu != null);
        if (action is { Enabled: true, Execute: not null }) { RootMenu.CloseMenu("键盘执行动作", true); e.Handled = true; await action.Execute(); }
    }
}
