namespace BetterCM.Services;

public static class MenuPlacement
{
    // Coordinates are physical pixels, including negative coordinates on secondary monitors.
    public static NativeMethods.RECT Cascade(NativeMethods.RECT parent, int rowTop, int width, int height, NativeMethods.RECT work, bool preferLeft = false, int gap = 4, int firstRowInset = 0)
    {
        int right = parent.Right + gap, left = parent.Left - width - gap;
        bool fitsRight = right + width <= work.Right - 4, fitsLeft = left >= work.Left + 4;
        int x = preferLeft && fitsLeft || !fitsRight && fitsLeft ? left : right;
        x = Math.Clamp(x, work.Left + 4, Math.Max(work.Left + 4, work.Right - width - 4));
        int y = Math.Clamp(rowTop - firstRowInset, work.Top + 4, Math.Max(work.Top + 4, work.Bottom - height - 4));
        return new() { Left = x, Top = y, Right = x + width, Bottom = y + height };
    }
}
