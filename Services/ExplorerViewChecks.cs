using System.Text.Json;
namespace BetterCM.Services;

// Read-only live regression: UI gestures are performed externally on test tabs.
internal static class ExplorerViewChecks
{
    internal static async Task<bool> RunAsync(string output, string expectedDirectory, string expectedFile, bool hold)
    {
        var results = new List<RegressionCheck>(); nint window = NativeMethods.GetForegroundWindow();
        try
        {
            using var capture = new ExplorerContextService();
            var context = await capture.CaptureAsync(window, []);
            bool exact = string.Equals(context.ShellViewPath.TrimEnd('\\'), expectedDirectory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) &&
                context.ShellTargets.SequenceEqual(new[] { expectedFile }, StringComparer.OrdinalIgnoreCase);
            results.Add(new("活动标签页捕获目录和精确选区", exact, JsonSerializer.Serialize(context)));
            using var binding = ShellViewBinding.Open(window, expectedDirectory, [expectedFile]);
            binding.ValidateLocation(); results.Add(new("执行绑定与活动标签页使用同一精确选区", true));
            if (hold)
            {
                await File.WriteAllTextAsync(output + ".ready.json", JsonSerializer.Serialize(new { Window = (long)window, Ready = exact }));
                var deadline = DateTime.UtcNow.AddSeconds(120);
                while (!File.Exists(output + ".continue") && DateTime.UtcNow < deadline) await Task.Delay(50);
                if (!File.Exists(output + ".continue")) throw new TimeoutException("等待切换测试标签超时");
                bool rejected = false;
                try { binding.ValidateLocation(); } catch (InvalidOperationException) { rejected = true; }
                results.Add(new("切换同目录标签后拒绝旧视图绑定", rejected));
            }
        }
        catch (Exception ex) { results.Add(new("活动视图检查异常", false, ex.ToString())); }
        bool passed = results.All(x => x.Passed);
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { Version = AppInfo.Version, Executable = Environment.ProcessPath, Passed = passed, Results = results }, new JsonSerializerOptions { WriteIndented = true }));
        return passed;
    }
}
