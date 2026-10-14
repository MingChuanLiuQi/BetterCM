namespace BetterCM.Services;

public enum MenuDismissReason { Pointer, Foreground }
public sealed record MenuRegion(nint Handle, NativeMethods.RECT Bounds);
public sealed record MenuObservation(nint Handle, NativeMethods.RECT Bounds, int Generation, uint StartedAt = 0, IReadOnlyList<MenuRegion>? Submenus = null)
{
    public bool ContainsHandle(nint handle) => Handle == handle || Submenus?.Any(x => x.Handle == handle) == true;
    public bool ContainsPoint(NativeMethods.POINT point) => !MenuDismissPolicy.IsOutside(Bounds, point) || Submenus?.Any(x => !MenuDismissPolicy.IsOutside(x.Bounds, point)) == true;
}
public static class MenuDismissPolicy
{
    public static bool IsOutside(NativeMethods.RECT r, NativeMethods.POINT p) => p.X < r.Left || p.X >= r.Right || p.Y < r.Top || p.Y >= r.Bottom;
    public static bool IsDismissPointerMessage(int message) => message is NativeMethods.WM_LBUTTONDOWN or NativeMethods.WM_RBUTTONDOWN or NativeMethods.WM_MBUTTONDOWN or NativeMethods.WM_XBUTTONDOWN or NativeMethods.WM_MOUSEWHEEL or NativeMethods.WM_MOUSEHWHEEL;
    public static bool IsCurrent(int visibleGeneration, bool visible, int requestGeneration) => visible && visibleGeneration == requestGeneration;
    public static bool IsNewEvent(uint started, uint eventTime) => unchecked((int)(eventTime - started)) > 0;
}
