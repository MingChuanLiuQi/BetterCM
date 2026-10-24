using System.Runtime.InteropServices;
using BetterCM.Config;
using BetterCM.Models;
namespace BetterCM.Services;

public static class FavoriteTransfer
{
    public static string Destination(FavoriteDestination favorite, DateTime date)
    {
        ActionRules.ValidateDirectory(favorite.Path);
        return favorite.DateSubfolder ? Path.Combine(favorite.Path, date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)) : favorite.Path;
    }
    internal static void ValidateSelection(SelectedContext context, string destination)
    {
        if (!context.HasFileTarget || !context.HasSelection) throw new InvalidOperationException("请先选择文件或文件夹");
        ActionRules.ValidateDirectory(destination);
        var paths = context.Items.Select(x => Path.TrimEndingDirectorySeparator(Path.GetFullPath(x.Path))).ToArray();
        if (paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Length) throw new InvalidOperationException("选区包含重复项目");
        foreach (var item in context.Items)
        {
            if (!Path.IsPathFullyQualified(item.Path)) throw new InvalidOperationException("源项目需要绝对路径");
            if (item.IsDirectory && ActionRules.IsWithin(destination, item.Path)) throw new IOException("不能将文件夹传输到它自身或内部目录");
            if (string.Equals(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(item.Path)), Path.TrimEndingDirectorySeparator(destination), StringComparison.OrdinalIgnoreCase)) throw new IOException("目标已经位于收藏目录内");
            if (context.Items.Any(other => other != item && other.IsDirectory && ActionRules.IsWithin(item.Path, other.Path))) throw new IOException("不能同时传输文件夹及其内部项目");
        }
    }
    public static async Task<string> TransferAsync(SelectedContext context, FavoriteDestination favorite, bool move)
    {
        var snapshot = context with { Items = context.Items.ToArray() };
        var settings = favorite.Clone();
        string destination = Destination(settings, DateTime.Today);
        ValidateSelection(snapshot, destination);
        using var shell = new ShellBridge();
        var result = await shell.TransferAsync(snapshot, settings, move);
        if (result.Aborted) return "Windows 文件操作已停止；已完成的项目保留，请检查目标目录。";
        if (!result.Success) throw new IOException(result.Error);
        return "Windows 文件操作已完成";
    }
    internal static void RunInHost(string[] paths, string baseDirectory, bool dateSubfolder, bool move, nint owner)
    {
        if (paths.Length is 0 or > 10000) throw new InvalidDataException("每次最多传输 10000 个选中项目");
        var favorite = new FavoriteDestination { Path = baseDirectory, DateSubfolder = dateSubfolder };
        string destination = Destination(favorite, DateTime.Today);
        var context = new SelectedContext { Items = paths.Select(path => new SelectedItem(path, (File.GetAttributes(path) & FileAttributes.Directory) != 0)).ToArray() };
        ValidateSelection(context, destination);
        if (!Directory.Exists(baseDirectory)) throw new DirectoryNotFoundException("收藏目录不可用，请在设置中重新选择：" + baseDirectory);
        if (dateSubfolder) Directory.CreateDirectory(destination);
        Transfer(paths, destination, move, owner);
    }
    // IFileOperation owns progress, conflict decisions, UAC and user-session undo records.
    internal static void Transfer(IReadOnlyList<string> sources, string destination, bool move, nint owner = 0)
    {
        var operation = (IFileOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(new("3AD05575-8857-4850-9277-11B85BDB8E09"), throwOnError: true)!)!;
        var items = new List<nint>(); nint folder = 0;
        try
        {
            operation.SetOperationFlags(0x20000000 | 0x0040 | 0x0200);
            if (owner != 0) operation.SetOwnerWindow(owner);
            folder = Item(destination);
            foreach (string path in sources)
            {
                var item = Item(path); items.Add(item);
                if (move) operation.MoveItem(item, folder, null, 0); else operation.CopyItem(item, folder, null, 0);
            }
            int result = operation.PerformOperations();
            operation.GetAnyOperationsAborted(out bool aborted);
            if (aborted) throw new OperationCanceledException("Windows 文件操作未全部完成；已完成项目不会自动回滚");
            Marshal.ThrowExceptionForHR(result);
        }
        finally
        {
            foreach (var item in items) Marshal.Release(item);
            if (folder != 0) Marshal.Release(folder);
            Marshal.FinalReleaseComObject(operation);
        }
    }
    private static nint Item(string path)
    {
        var iid = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");
        Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(path, 0, ref iid, out var item));
        return item;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string path, nint binding, ref Guid iid, out nint item);
    [ComImport, Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOperation
    {
        void Advise(nint sink, out uint cookie);
        void Unadvise(uint cookie);
        void SetOperationFlags(uint flags);
        void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);
        void SetProgressDialog(nint dialog);
        void SetProperties(nint properties);
        void SetOwnerWindow(nint owner);
        void ApplyPropertiesToItem(nint item);
        void ApplyPropertiesToItems(nint items);
        void RenameItem(nint item, [MarshalAs(UnmanagedType.LPWStr)] string name, nint sink);
        void RenameItems(nint items, [MarshalAs(UnmanagedType.LPWStr)] string name);
        void MoveItem(nint item, nint folder, [MarshalAs(UnmanagedType.LPWStr)] string? name, nint sink);
        void MoveItems(nint items, nint folder);
        void CopyItem(nint item, nint folder, [MarshalAs(UnmanagedType.LPWStr)] string? name, nint sink);
        void CopyItems(nint items, nint folder);
        void DeleteItem(nint item, nint sink);
        void DeleteItems(nint items);
        void NewItem(nint folder, uint attributes, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string? template, nint sink);
        [PreserveSig] int PerformOperations();
        void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
    }
}
