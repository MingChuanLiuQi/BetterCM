using System;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BetterCM.Services;

public static class IconHelper
{
    public static ImageSource? GetIconForPath(string path, bool isDirectory)
    {
        try
        {
            var shinfo = new NativeMethods.SHFILEINFO();
            uint flags = NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_SMALLICON;
            uint attr = 0;

            if (!File.Exists(path) && !Directory.Exists(path))
            {
                flags |= NativeMethods.SHGFI_USEFILEATTRIBUTES;
                attr = isDirectory ? NativeMethods.FILE_ATTRIBUTE_DIRECTORY : NativeMethods.FILE_ATTRIBUTE_NORMAL;
            }

            var res = NativeMethods.SHGetFileInfo(path, attr, ref shinfo, (uint)System.Runtime.InteropServices.Marshal.SizeOf(shinfo), flags);
            if (res != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
            {
                try
                {
                    var bitmap = Imaging.CreateBitmapSourceFromHIcon(
                        shinfo.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    bitmap.Freeze();
                    return bitmap;
                }
                finally
                {
                    NativeMethods.DestroyIcon(shinfo.hIcon);
                }
            }
        }
        catch
        {
            // Ignore icon extraction errors
        }

        return null;
    }
}
