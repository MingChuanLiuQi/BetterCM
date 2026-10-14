using System.Text.Json;
using Microsoft.Win32;
using BetterCM.Models;
namespace BetterCM.Services;

// Temporary, uniquely named static verbs launch this executable as a finite argv probe.
// No terminal commands, user documents or existing associations are changed.
internal static class ShellLaunchChecks
{
    internal static async Task RunAsync(string fixture, Action<string, bool, string?> check)
    {
        string root = Path.Combine(fixture, "launch-中文 & 空格-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        string file = Path.Combine(root, "选中 & 文件.txt"); File.WriteAllText(file, "native launch fixture");
        string verb = "BetterCM.QA." + Guid.NewGuid().ToString("N");
        var registrations = new List<string>();
        try
        {
            foreach (var pair in new[] { (Type: "Directory", Token: "%1"), (Type: "*", Token: "%1"), (Type: "Directory\\Background", Token: "%V") })
            {
                string report = Path.Combine(root, (pair.Type == "*" ? "File" : pair.Type.Replace('\\', '-')) + "-result.json");
                string path = "Software\\Classes\\" + pair.Type + "\\shell\\" + verb;
                using var key = Registry.CurrentUser.CreateSubKey(path); registrations.Add(path);
                key.SetValue("", verb);
                using var command = key.CreateSubKey("command");
                command.SetValue("", string.Join(" ", new[] { Environment.ProcessPath!, "--verb-probe", report, pair.Token }.Select(BackgroundProcess.Quote)));
            }
            NativeMethods.SHChangeNotify(0x08000000, 0, 0, 0); // SHCNE_ASSOCCHANGED / SHCNF_IDLIST.
            using var bridge = new ShellBridge();
            foreach (var item in new[]
            {
                (Name: "文件夹原生启动传递完整物理路径", Context: new SelectedContext { CurrentDirectory = root, Items = [new(root, true)] }, Report: "Directory-result.json", Expected: root),
                (Name: "文件原生启动保留中文空格及特殊字符参数", Context: new SelectedContext { CurrentDirectory = root, Items = [new(file, false)] }, Report: "File-result.json", Expected: file),
                (Name: "文件夹背景原生启动以当前目录展开百分号V", Context: new SelectedContext { CurrentDirectory = root, ShellBackground = true }, Report: "Directory-Background-result.json", Expected: root)
            })
                await InvokeProbe(bridge, item.Context, verb, Path.Combine(root, item.Report), item.Expected, item.Name, check);
            // Test the remote view's RPC service path separately: Explorer can cache its
            // desktop associations, while the physical background probe above is local.
            await ComServiceChecks.RunAsync(check);
        }
        finally
        {
            foreach (var path in registrations) Registry.CurrentUser.DeleteSubKeyTree(path, false);
            NativeMethods.SHChangeNotify(0x08000000, 0, 0, 0);
        }
    }
    private static async Task InvokeProbe(ShellBridge bridge, SelectedContext context, string verb, string report, string expected, string name, Action<string, bool, string?> check)
    {
        var menu = await bridge.OpenAsync(context, deferSubmenus: true);
        var command = menu.Items?.FirstOrDefault(x => x.Verb.Equals(verb, StringComparison.OrdinalIgnoreCase) || x.Title == verb);
        if (!menu.Success || command == null) { check(name, false, menu.Error ?? "测试原生菜单项缺失"); return; }
        var result = await bridge.InvokeAsync(menu.Session!, command.Command);
        for (int i = 0; i < 200 && !File.Exists(report); i++) await Task.Delay(25);
        bool valid = false;
        if (File.Exists(report))
        {
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(report));
            valid = json.RootElement.GetProperty("Arguments").EnumerateArray().Single().GetString()?.TrimEnd('\\').Equals(expected.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) == true;
        }
        check(name, result.Success && valid, result.Error ?? report);
    }
}
