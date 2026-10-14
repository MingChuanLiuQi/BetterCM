using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Shell;
using Microsoft.Win32;
using System.Runtime.CompilerServices;
using BetterCM.Views;
namespace BetterCM.Services;
public static class ThemeService
{
    private static string _material = "mica";
    private static bool _light, _useMaterial, _highContrast;
    private static readonly ConditionalWeakTable<Window, MenuBackdrop> MenuBackdrops = new();
    public static bool HasStableMenuBackdrop(Window window) => MenuBackdrops.TryGetValue(window, out var backdrop) && backdrop.KeepsMaterialWithoutFocus;
    public static string Diagnostics => $"材质：{_material}；系统材质：{(_useMaterial ? "可用" : "纯色回退")}；Windows build：{Environment.OSVersion.Version.Build}";
    public static void Apply(string mode, string material = "mica")
    {
        _material = material;
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        bool light = mode == "light" || mode == "system" && key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        ApplyResolved(light, material, SystemParameters.HighContrast, key?.GetValue("EnableTransparency") is not 0);
    }
    // Exercise the production palette without modifying the user's Windows accessibility settings.
    internal static void ApplyResolved(bool light, string material, bool highContrast, bool transparency)
    {
        _material = material; _highContrast = highContrast;
        _light = light;
        _useMaterial = material != "solid" && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621) && !highContrast && transparency;
        var colors = light
            ? new[] { "#F3F3F3", "#FFFFFF", "#FAFAFA", "#E0E0E0", "#1A1A1A", "#626262", "#EAEAEA", "#005FB8", "#F9F9F9", "#868686" }
            : new[] { "#202020", "#2B2B2B", "#1F1F1F", "#404040", "#FFFFFF", "#B6B6B6", "#383838", "#60CDFF", "#292929", "#9D9D9D" };
        var names = new[] { "WindowBg", "CardBg", "FieldBg", "BorderBrush", "TextBrush", "MutedBrush", "HoverBrush", "AccentBrush", "MenuBg", "ScrollThumbBrush" };
        for (int i = 0; i < names.Length; i++) SetBrush(names[i], colors[i]);
        SetBrush("MenuSurfaceBg", light ? "#F9F9F9" : "#292929");
        SetBrush("OnAccentBrush", light ? "#FFFFFF" : "#1A1A1A");
        SetBrush("GroupRowBg", light ? "#10005FB8" : "#1860CDFF");
        SetBrush("SourceBadgeBg", light ? "#18005FB8" : "#2860CDFF");
        SetBrush("SourceBadgeText", light ? "#005FB8" : "#A3DEFF");
        SetBrush("ErrorBrush", light ? "#C42B1C" : "#FF9999");
        if (_useMaterial)
        {
            SetBrush("WindowBg", "#00000000"); SetBrush("MenuSurfaceBg", light ? "#0CFFFFFF" : "#08202020");
            SetBrush("CardBg", light ? "#C0FFFFFF" : "#99323232"); SetBrush("FieldBg", light ? "#B8FFFFFF" : "#80202020");
            SetBrush("BorderBrush", light ? "#20000000" : "#20FFFFFF"); SetBrush("HoverBrush", light ? "#99FFFFFF" : "#70494949");
        }
        if (highContrast)
        {
            Application.Current.Resources["TextBrush"] = SystemColors.WindowTextBrush;
            Application.Current.Resources["MutedBrush"] = SystemColors.WindowTextBrush;
            Application.Current.Resources["WindowBg"] = Application.Current.Resources["MenuBg"] = Application.Current.Resources["CardBg"] = Application.Current.Resources["FieldBg"] = SystemColors.WindowBrush;
            Application.Current.Resources["MenuSurfaceBg"] = SystemColors.WindowBrush;
            Application.Current.Resources["HoverBrush"] = SystemColors.HighlightBrush;
            Application.Current.Resources["AccentBrush"] = SystemColors.HighlightBrush;
            Application.Current.Resources["OnAccentBrush"] = SystemColors.HighlightTextBrush;
            Application.Current.Resources["BorderBrush"] = Application.Current.Resources["ScrollThumbBrush"] = SystemColors.WindowTextBrush;
            Application.Current.Resources["GroupRowBg"] = Application.Current.Resources["SourceBadgeBg"] = SystemColors.WindowBrush;
            Application.Current.Resources["SourceBadgeText"] = SystemColors.WindowTextBrush;
            Application.Current.Resources["ErrorBrush"] = SystemColors.WindowTextBrush;
        }
        foreach (Window window in Application.Current.Windows) RefreshWindow(window);
    }
    private static void SetBrush(string name, string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); brush.Freeze(); Application.Current.Resources[name] = brush;
    }
    public static void Attach(Window window)
    {
        window.SourceInitialized += (_, _) => RefreshWindow(window);
        // Popup material and glass are established before Show. Reapplying them after the
        // first content frame recreates the DWM surface near the end of the opening animation.
        if (window is not ContextMenuWindow) window.ContentRendered += (_, _) => RefreshWindow(window);
        window.Closed += (_, _) => { if (MenuBackdrops.TryGetValue(window, out var backdrop)) backdrop.Dispose(); MenuBackdrops.Remove(window); };
    }
    private static void RefreshWindow(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == 0) return;
        int dark = _light ? 0 : 1;
        NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        int corner = NativeMethods.DWMWCP_ROUND; // DWMWCP_ROUND: let Windows 11 render corners on an opaque HWND.
        NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
        bool controlled = false;
        if (window is ContextMenuWindow menuWindow)
        {
            int disableNativeTransitions = 1;
            menuWindow.NativeTransitionResult = NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_TRANSITIONS_FORCEDISABLED, ref disableNativeTransitions, sizeof(int));
            var controller = MenuBackdrops.GetOrCreateValue(window);
            if (_useMaterial) controlled = controller.Apply(handle, _material, _light); else controller.Dispose();
        }
        int backdrop = _useMaterial && !controlled ? (_material == "acrylic" ? NativeMethods.DWMSBT_TRANSIENTWINDOW : NativeMethods.DWMSBT_TABBEDWINDOW) : NativeMethods.DWMSBT_NONE;
        int result = NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int)); // DWMWA_SYSTEMBACKDROP_TYPE
        bool active = _useMaterial && (controlled || result >= 0);
        if (WindowChrome.GetWindowChrome(window) is { } chrome) chrome.GlassFrameThickness = active ? new Thickness(-1) : new Thickness(1);
        var margins = new NativeMethods.MARGINS { Left = active ? -1 : 0, Right = active ? -1 : 0, Top = active ? -1 : 0, Bottom = active ? -1 : 0 };
        int frameResult = NativeMethods.DwmExtendFrameIntoClientArea(handle, ref margins);
        active &= frameResult >= 0;
        if (HwndSource.FromHwnd(handle)?.CompositionTarget is { } target) target.BackgroundColor = active ? Colors.Transparent : _highContrast ? SystemColors.WindowColor : (_light ? Color.FromRgb(243, 243, 243) : Color.FromRgb(32, 32, 32));
        window.Resources.Remove("WindowBg"); window.Resources.Remove("MenuSurfaceBg");
        if (_useMaterial && !active)
        {
            window.Resources["WindowBg"] = new SolidColorBrush(_light ? Color.FromRgb(243, 243, 243) : Color.FromRgb(32, 32, 32));
            window.Resources["MenuSurfaceBg"] = new SolidColorBrush(_light ? Color.FromRgb(249, 249, 249) : Color.FromRgb(41, 41, 41));
        }
    }
}
