using System.Runtime.InteropServices;
namespace BetterCM.Services;

internal static class ShellClipboard
{
    // Probe formats without fetching/deserializing another application's IDataObject.
    // Preferred DropEffect is only transfer metadata and cannot identify a file payload.
    private static readonly uint[] TransferFormats = [15, RegisterClipboardFormat("Shell IDList Array"), RegisterClipboardFormat("FileGroupDescriptorW"), RegisterClipboardFormat("FileGroupDescriptor")];
    internal static bool HasTransferData => TransferFormats.Any(IsClipboardFormatAvailable);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterClipboardFormat(string name);
}
