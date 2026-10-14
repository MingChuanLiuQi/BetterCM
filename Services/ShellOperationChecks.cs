using System.Runtime.InteropServices;
using BetterCM.Models;
namespace BetterCM.Services;

// Real Shell file transfers, restricted to freshly created fixtures. Never empties the user's bin.
internal static class ShellOperationChecks
{
    public static async Task RunAsync(string fixtureRoot, Action<string, bool, string?> check)
    {
        string root = Path.Combine(fixtureRoot, "transfer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string source = Path.Combine(root, "source"), copied = Path.Combine(root, "copied"), moved = Path.Combine(root, "moved");
        foreach (var folder in new[] { source, copied, moved }) Directory.CreateDirectory(folder);
        string file = Path.Combine(source, "中文 & 剪切.txt");
        await File.WriteAllTextAsync(file, "BetterCM Shell transfer fixture");
        // Snapshot data now: a live OleGetClipboard proxy points back to the current clipboard
        // and must never be passed to OleSetClipboard (that creates a recursive data provider).
        var previous = new System.Windows.DataObject();
        var original = System.Windows.Clipboard.GetDataObject();
        if (original != null)
            foreach (var format in original.GetFormats(false))
            {
                try
                {
                    var data = original.GetData(format, false);
                    if (data is MemoryStream stream) data = new MemoryStream(stream.ToArray());
                    if (data != null && !Marshal.IsComObject(data)) previous.SetData(format, data, false);
                }
                catch (Exception ex) { AppLog.Write("test-clipboard-snapshot", ex); }
            }
        try
        {
            var textWithMetadata = new System.Windows.DataObject();
            textWithMetadata.SetText("clipboard metadata fixture");
            textWithMetadata.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(1)), false);
            System.Windows.Clipboard.SetDataObject(textWithMetadata, true);
            check("文本与 DropEffect 元数据不误判为文件传输", !ShellClipboard.HasTransferData, null);
            foreach (string format in new[] { "FileGroupDescriptorW", "FileGroupDescriptor" })
            {
                var virtualFiles = new System.Windows.DataObject();
                virtualFiles.SetData(format, new MemoryStream(new byte[4]), false);
                System.Windows.Clipboard.SetDataObject(virtualFiles, true);
                check("虚拟文件格式能力探测保留兼容：" + format, ShellClipboard.HasTransferData, null);
            }
            using var bridge = new ShellBridge();
            var executor = new ActionExecutor(bridge);
            SelectedContext Item(string path) => new() { Items = [new(path, false)], CurrentDirectory = Path.GetDirectoryName(path)! };
            SelectedContext Background(string path) => new() { CurrentDirectory = path, ShellBackground = true };
            await executor.ExecuteAsync("copy", Item(file));
            check("真实 Shell 文件复制仍识别为文件传输", ShellClipboard.HasTransferData, null);
            var pasteMenu = await bridge.OpenAsync(Background(copied));
            File.WriteAllText(Path.Combine(root, "paste-menu.json"), System.Text.Json.JsonSerializer.Serialize(new { pasteMenu, Formats = System.Windows.Clipboard.GetDataObject()?.GetFormats() }));
            await executor.ExecuteAsync("paste", Background(copied), existingShell: pasteMenu);
            string copy = Path.Combine(copied, Path.GetFileName(file));
            await WaitForAsync(() => File.Exists(copy));
            check("真实 Shell 复制粘贴保留源文件并生成相同内容", File.Exists(file) && File.Exists(copy) && await File.ReadAllTextAsync(file) == await File.ReadAllTextAsync(copy), null);
            await executor.ExecuteAsync("cut", Item(copy));
            // Give the isolated clipboard producer time to exit; paste must still work.
            await Task.Delay(900);
            await executor.ExecuteAsync("paste", Background(moved));
            string destination = Path.Combine(moved, Path.GetFileName(file));
            await WaitForAsync(() => File.Exists(destination) && !File.Exists(copy));
            check("真实 Shell 剪切后跨进程粘贴移动文件且移除源项", File.Exists(destination) && !File.Exists(copy) && await File.ReadAllTextAsync(destination) == await File.ReadAllTextAsync(file), null);
            var bin = await bridge.OpenAsync(new() { ShellParsingPaths = ["::{645FF040-5081-101B-9F08-00AA002F954E}"], DisplayName = "回收站" });
            check("回收站命名空间菜单可读取并保持原始命令身份", bin.Success && bin.Items?.Count > 0 && bin.Items.Select(x => x.Command).Distinct().Count() == bin.Items.Count, bin.Error);
            File.WriteAllText(Path.Combine(root, "recycle-menu.json"), System.Text.Json.JsonSerializer.Serialize(bin));
            check("回收站原生清空入口保留命令及启用状态", bin.Success && bin.Items!.Any(x => x.Verb.Equals("empty", StringComparison.OrdinalIgnoreCase)), bin.Error);
        }
        finally
        {
            System.Windows.Clipboard.SetDataObject(previous, true);
        }
    }
    private static async Task WaitForAsync(Func<bool> complete)
    {
        for (int i = 0; i < 100 && !complete(); i++) await Task.Delay(50);
    }
}
