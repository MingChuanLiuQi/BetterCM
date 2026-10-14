namespace BetterCM.Services;

internal static class MenuWindowSize
{
    // WM_SIZE carries two unsigned 16-bit dimensions even in a 64-bit LPARAM.
    internal static nint Pack(int width, int height)
        => (nint)(((uint)Math.Clamp(height, 0, ushort.MaxValue) << 16) | (uint)Math.Clamp(width, 0, ushort.MaxValue));
}
