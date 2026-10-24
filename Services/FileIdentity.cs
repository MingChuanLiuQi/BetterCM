using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace BetterCM.Services;

internal sealed record FileIdentity(uint Volume, ulong Index, bool Directory, long Length, long Created, long Modified)
{
    internal bool SameObject(FileIdentity other) => Volume == other.Volume && Index == other.Index && Directory == other.Directory;
    internal static FileIdentity Read(string path, bool directory)
    {
        string nativePath = path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path : path.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + path[2..] : @"\\?\" + path;
        using var handle = CreateFileW(nativePath, 0x80, 7, 0, 3, 0x02200000, 0);
        if (handle.IsInvalid || !GetFileInformationByHandle(handle, out var info)) throw new IOException("无法读取文件标识：" + path, new Win32Exception(Marshal.GetLastWin32Error()));
        if ((info.Attributes & 0x400) != 0 || ((info.Attributes & 0x10) != 0) != directory) throw new IOException("目标类型已变化或属于链接：" + path);
        return new(info.Volume, ((ulong)info.IndexHigh << 32) | info.IndexLow, directory, directory ? 0 : ((long)info.SizeHigh << 32) | info.SizeLow,
            ((long)info.CreatedHigh << 32) | info.CreatedLow, ((long)info.ModifiedHigh << 32) | info.ModifiedLow);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Information
    {
        public uint Attributes, CreatedLow, CreatedHigh, AccessLow, AccessHigh, ModifiedLow, ModifiedHigh, Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, nint security, uint creation, uint flags, nint template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out Information information);
}
