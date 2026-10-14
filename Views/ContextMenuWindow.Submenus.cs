using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Services;

namespace BetterCM.Views;

public partial class ContextMenuWindow
{
    private ContextMenuWindow? _parentMenu, _submenu;
    private ContextMenuWindow? _cachedSubmenu;
    private bool _windowClosed;
    private string _submenuId = "";
    private bool _opensLeft;
    private bool _focusOnPresentation;
    private readonly HoverExpansion _groupHover = new();
    private sealed class GroupLoad(int generation, int request, bool focus)
    {
        internal readonly int Generation = generation;
        internal int Request = request;
        internal bool Focus = focus;
    }
    private readonly Dictionary<int, GroupLoad> _loadingGroups = [];
    private GroupLoad? _activeGroupLoad;
    private int _groupRequest;
    private ContextMenuWindow RootMenu => _parentMenu?.RootMenu ?? this;
    internal ContextMenuWindow? OpenSubmenu => _submenu;
    internal IReadOnlyList<MenuEntry> VisibleRows => ActionsList.Items.Cast<MenuEntry>().ToList();
    public object? CascadeLayout => _submenu == null ? null : new
    {
        Group = _submenuId, Width = _submenu.Width, Height = _submenu.Height,
        Rows = _submenu.VisibleRows.Count, OpensLeft = _submenu._opensLeft,
        Material = ThemeService.MenuMaterial(_submenu), HostBackdrop = ThemeService.MenuHostBackdropEnabled(_submenu),
        Regions = SubmenuRegions().Select(x => new { Handle = (long)x.Handle, x.Bounds.Left, x.Bounds.Top, x.Bounds.Right, x.Bounds.Bottom }).ToArray(), Layout = _submenu.LayoutDiagnostics,
        Child = _submenu.CascadeLayout
    };

    private List<MenuRegion> SubmenuRegions()
    {
        var regions = new List<MenuRegion>();
        for (var child = _submenu; child?.IsOpen == true; child = child._submenu)
        {
            var handle = new WindowInteropHelper(child).Handle;
            if (NativeMethods.GetWindowRect(handle, out var bounds)) regions.Add(new(handle, bounds));
        }
        return regions;
    }
    private bool ContainsMenuHandle(nint handle)
        => new WindowInteropHelper(this).Handle == handle || SubmenuRegions().Any(x => x.Handle == handle);

    private void CloseSubmenu()
    {
        _groupRequest++;
        _activeGroupLoad = null;
        _groupHover.Cancel();
        var child = _submenu;
        _submenu = null; _submenuId = "";
        if (child == null) return;
        child.CloseMenu("关闭子菜单", true);
        RootMenu.PublishBounds();
    }
    private void DisposeSubmenus()
    {
        CloseSubmenu();
        var child = _cachedSubmenu; _cachedSubmenu = null;
        if (child != null && !child._windowClosed) child.Close();
    }
    private void Action_Enter(object sender, MouseEventArgs e)
    {
        if (!IsOpen || sender is not Button { Tag: MenuEntry entry } button) return;
        int generation = _generation;
        _groupHover.Schedule(_config.SubmenuHoverDelayMs, () =>
        {
            if (generation != _generation || !IsOpen || !button.IsMouseOver) return;
            if (entry.IsGroup) OpenGroup(entry); else CloseSubmenu();
        });
    }
    private void Action_Leave(object sender, MouseEventArgs e)
    {
        _groupHover.Cancel();
        if (_submenu == null && _activeGroupLoad?.Focus != true) _groupRequest++;
    }

    internal void OpenGroup(MenuEntry entry, bool focus = false)
    {
        _groupHover.Cancel();
        if (!IsOpen || !entry.IsGroup || !entry.Enabled) return;
        if (entry.DeferredSubmenu is int token && RootMenu._nativeLoad != null)
        {
            if (_loadingGroups.TryGetValue(token, out var loading))
            {
                if (_activeGroupLoad != loading || loading.Request != _groupRequest)
                {
                    CloseSubmenu(); loading.Request = _groupRequest; _activeGroupLoad = loading;
                }
                loading.Focus |= focus;
            }
            else _ = LoadGroupAsync(entry, token, focus);
            return;
        }
        if (entry.Children.Count == 0) return;
        if (_submenu?.IsOpen == true && _submenuId == entry.Id)
        {
            if (focus) _submenu.FocusFirst();
            return;
        }
        ActionsList.ScrollIntoView(entry); ActionsList.UpdateLayout();
        if (ActionsList.ItemContainerGenerator.ContainerFromItem(entry) is not FrameworkElement row) return;
        var top = row.PointToScreen(new Point(0, 0));
        var parentHandle = new WindowInteropHelper(this).Handle;
        if (!NativeMethods.GetWindowRect(parentHandle, out var parentBounds)) return;
        CloseSubmenu();
        _submenu = _cachedSubmenu ??= new ContextMenuWindow { _parentMenu = this, Owner = this, ShowActivated = false, ShowInTaskbar = ShowInTaskbar };
        _submenu.Title = "BetterCM · " + entry.Title;
        _submenuId = entry.Id;
        _submenu.ShowCascade(entry.Children, _config, parentBounds, (int)Math.Round(top.Y), _work, _scale, _opensLeft);
        RootMenu.PublishBounds();
        if (focus) _submenu.FocusFirst();
    }
    private async Task LoadGroupAsync(MenuEntry entry, int token, bool focus)
    {
        CloseSubmenu();
        var loading = new GroupLoad(RootMenu._generation, _groupRequest, focus);
        _loadingGroups.Add(token, loading); _activeGroupLoad = loading;
        try
        {
            var page = await RootMenu._nativeLoad!(token);
            if (RootMenu._generation != loading.Generation || !RootMenu.IsOpen ||
                !_loadingGroups.TryGetValue(token, out var owner) || owner != loading) return;
            // A valid same-session response may complete after another group. Preserve
            // commands discovered by the later response instead of regressing the table.
            RootMenu._nativeState.AddCommands(page.Actions);
            RootMenu._nativeMenu = NativeMenuTree.Replace(RootMenu._nativeMenu!, token, page.Menu);
            entry.Children = NativeMenuComposer.ComposeIndexed(page.Menu, RootMenu._nativeState.Commands); entry.DeferredSubmenu = null;
            if (_activeGroupLoad == loading && loading.Request == _groupRequest && IsOpen) OpenGroup(entry, loading.Focus);
        }
        catch (OperationCanceledException) { /* A superseded menu request is not a read failure. */ }
        catch (Exception ex)
        {
            if (RootMenu._generation == loading.Generation && IsOpen && _activeGroupLoad == loading &&
                loading.Request == _groupRequest && _loadingGroups.TryGetValue(token, out var current) && current == loading)
                RootMenu.ShowLoadError(ex);
        }
        finally
        {
            if (_loadingGroups.TryGetValue(token, out var current) && current == loading) _loadingGroups.Remove(token);
            if (_activeGroupLoad == loading) _activeGroupLoad = null;
        }
    }

    private void ShowCascade(IReadOnlyList<MenuEntry> rows, AppConfig config, NativeMethods.RECT parentBounds, int rowTop, NativeMethods.RECT work, double scale, bool preferLeft)
    {
        _closing = false; IsHitTestVisible = Surface.IsHitTestVisible = true;
        _firstFrame.Cancel(); CancelExpansion(); _animator.Reset();
        _focusOnPresentation = false; _activeGroupLoad = null; _loadingGroups.Clear(); _groupRequest++;
        _opening = true; _generation = RootMenu._generation; _config = config; _expanded = true;
        _work = work; _scale = scale; ApplyMetrics(config);
        // Submenus contain only their rows; do not repeat title, search or the root footer.
        var grid = (Grid)Surface.Child;
        foreach (FrameworkElement child in grid.Children) if (child != ActionsList) child.Visibility = Visibility.Collapsed;
        ActionsList.ItemsSource = rows;
        FindScroll(ActionsList)?.ScrollToHome();
        var handle = new WindowInteropHelper(this).EnsureHandle();
        if (_contentSource != null && _contentSource.RootVisual == null) _contentSource.RootVisual = Surface;
        NativeMethods.SetWindowPos(handle, 0, parentBounds.Left, rowTop, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        _scale = Math.Max(1, NativeMethods.GetDpiForWindow(handle) / 96.0);
        MaxHeight = Math.Max(1, Math.Min(640, (work.Bottom - work.Top - 12) / _scale));
        Height = MeasureContent();
        ActionsList.UpdateLayout();
        // Align the first actual row, rather than the submenu's border, to its parent row.
        double inset = Surface.Padding.Top + Surface.BorderThickness.Top;
        if (ActionsList.ItemContainerGenerator.ContainerFromIndex(0) is FrameworkElement first)
            inset = first.TransformToAncestor(Surface).Transform(new Point()).Y;
        var placement = MenuPlacement.Cascade(parentBounds, rowTop, (int)Math.Ceiling(Width * _scale), (int)Math.Ceiling(Height * _scale), work, preferLeft,
            (int)Math.Round(MenuMetrics.CascadeGap * _scale), (int)Math.Round(inset * _scale));
        _x = placement.Left; _y = placement.Top; _opensLeft = placement.Left < parentBounds.Left;
        ResizeNative(Height);
        var animation = config.Clone();
        animation.EnableAnimations = ExpansionAnimator.Enabled(config, config.GroupExpansionAnimation);
        animation.OpenAnimation = config.GroupExpansionAnimation; animation.OpenAnimationMs = config.GroupExpansionMs;
        bool held = SurfaceAnimator.OpeningAnimated(animation) && _firstFrame.Hold(handle);
        if (held) _animator.PrepareOpening(animation, _opensLeft ? -1 : 1);
        else _animator.Play(animation, true, horizontalDirection: _opensLeft ? -1 : 1);
        Show(); UpdateLayout(); _opening = false; _shownAt = RootMenu._shownAt;
        if (held) _firstFrame.Present(Dispatcher, () => { if (IsOpen) { _animator.Play(animation, true, horizontalDirection: _opensLeft ? -1 : 1); if (_focusOnPresentation) FocusFirst(); } });
        ActionsList.SelectedIndex = -1;
    }
    private void FocusFirst()
    {
        if (_firstFrame.Pending) { _focusOnPresentation = true; return; }
        _focusOnPresentation = false;
        NativeMethods.SetForegroundWindow(new WindowInteropHelper(this).Handle); Activate(); FocusContent(); ActionsList.Focus();
        ActionsList.SelectedItem = VisibleRows.FirstOrDefault(x => !x.IsSeparator && x.Enabled);
    }
    private void ReturnToParent()
    {
        var parent = _parentMenu!;
        parent.CloseSubmenu();
        NativeMethods.SetForegroundWindow(new WindowInteropHelper(parent).Handle); parent.Activate(); parent.FocusContent(); parent.ActionsList.Focus();
        // Hiding an owned window can restore its owner's earlier text-box focus later.
        // Apply menu-row focus after that activation work has returned to the dispatcher.
        _ = parent.Dispatcher.BeginInvoke(() => { if (parent.IsOpen) { parent.FocusContent(); parent.ActionsList.Focus(); } }, System.Windows.Threading.DispatcherPriority.Input);
    }
}
