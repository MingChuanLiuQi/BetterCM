using System.Diagnostics;
using System.Text.Json;
using BetterCM.Config;
using BetterCM.Models;
namespace BetterCM.Services;

public static class RobustnessChecks
{
    public static async Task RunAsync(string output)
    {
        var results = new List<object>(); bool passed = true;
        void Check(string name, bool valid, string? detail = null) { results.Add(new { Name = name, Passed = valid, Detail = detail }); passed &= valid; }
        try
        {
            using (var overlap = new ShellBridge(TimeSpan.FromSeconds(5), true))
            {
                var fixtureContext = new SelectedContext { CurrentDirectory = AppContext.BaseDirectory };
                var opened = await overlap.OpenAsync(fixtureContext);
                var reading = overlap.ExpandAsync(opened.Session!, 350); await Task.Delay(40);
                var execution = await overlap.InvokeAsync(opened.Session!, 0); var superseded = await reading;
                Check("延迟 expand 与 invoke 共享串行连接，动作成功且旧读取明确取消", execution.Success && superseded.Superseded);
                var afterExecution = await overlap.ExpandAsync(opened.Session!, 0);
                Check("执行后的旧会话 expand 返回取消而非真实读取失败", !afterExecution.Success && afterExecution.Superseded);
                var nextMenu = await overlap.OpenAsync(fixtureContext);
                var pending = overlap.ExpandAsync(nextMenu.Session!, 350); await Task.Delay(40);
                var replacement = await overlap.OpenAsync(fixtureContext); var oldResult = await pending;
                Check("新 open 取代在飞 expand，响应不混入新会话", replacement.Success && oldResult.Superseded);
                var terminated = await overlap.ExpandAsync(replacement.Session!, -3);
                Check("当前测试宿主真实断开仍返回读取失败", !terminated.Success && !terminated.Superseded);
                var repairedConnection = await overlap.OpenAsync(fixtureContext);
                Check("读取连接断开后下一次 open 直接恢复，不复用死连接", repairedConnection.Success && repairedConnection.Session != replacement.Session);
            }
            using var bridge = new ShellBridge(TimeSpan.FromSeconds(30), true);
            uint startupFlags = await bridge.TestStartupFlagsAsync();
            Check("Shell 辅助宿主关闭启动光标反馈且允许原生对话框", (startupFlags & BackgroundProcess.NoFeedback) != 0 && (startupFlags & 1) == 0, $"flags=0x{startupFlags:X}");
            string logScope = "log-regression-" + Guid.NewGuid().ToString("N");
            var probes = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => bridge.TestLoggingAsync(12, logScope)));
            int logLines = File.ReadLines(Path.Combine(ConfigStore.DataDirectory, "BetterCM.log")).Count(x => x.Contains(logScope + ":", StringComparison.Ordinal));
            Check("三个独立宿主并发写日志无丢行", probes.All(x => x.Success) && logLines == 36, $"lines={logLines}/36");
            var context = new SelectedContext { CurrentDirectory = AppContext.BaseDirectory };
            var first = await bridge.OpenAsync(context); await bridge.PrepareNextAsync();
            var invocation = bridge.InvokeAsync(first.Session!, 40);
            var duplicateInvocation = await bridge.InvokeAsync(first.Session!, 1);
            Check("已移交执行的会话不能再次 invoke，即使宿主命令仍在运行", !duplicateInvocation.Success);
            int? id = bridge.InvokingProcessId;
            var watch = Stopwatch.StartNew(); var second = await bridge.OpenAsync(context); watch.Stop();
            Check("40 秒 verb 不阻塞第二次 open", second.Success && watch.ElapsedMilliseconds < 50, $"{watch.Elapsed.TotalMilliseconds:F2} ms");
            var timings = new List<double>();
            for (int i = 0; i < 50; i++) { watch.Restart(); var result = await bridge.OpenAsync(context); timings.Add(watch.Elapsed.TotalMilliseconds); Check("并发时响应关联正确 " + i, result.Success && result.RequestId != null); }
            Check("50 次连续 open 无超时", timings.Max() < 2000, $"p50={timings.Order().ElementAt(25):F2} ms, max={timings.Max():F2} ms");
            var timeout = await invocation;
            using var host = id is int pid ? Process.GetProcessById(pid) : null;
            Check("30 秒超时只报告通信失败且宿主存活", !timeout.Success && host is { HasExited: false }, timeout.Error);
            bridge.Dispose();
            Check("Dispose 不终止超时用户任务宿主", host is { HasExited: false });
            using var capture = new ExplorerContextService(true);
            watch.Restart(); var stalled = await capture.CaptureAsync(0, ["stall"]);
            Check("卡住捕获在 2 秒内返回降级结果", !stalled.HasTarget && watch.ElapsedMilliseconds < 2000, $"{watch.Elapsed.TotalMilliseconds:F2} ms");
            watch.Restart(); var recovered = await capture.CaptureAsync(0, []);
            Check("后续捕获不被旧 UIA 阻塞", recovered.HasTarget && watch.ElapsedMilliseconds < 2000, $"{watch.Elapsed.TotalMilliseconds:F2} ms");
            Check("上下文辅助宿主关闭启动光标反馈", (capture.LastStartupFlags & BackgroundProcess.NoFeedback) != 0, $"flags=0x{capture.LastStartupFlags:X}");
            using var cancellation = new CancellationTokenSource(100);
            bool canceled = false;
            try { await capture.CaptureAsync(0, ["stall"], cancellation: cancellation.Token); } catch (OperationCanceledException) { canceled = true; }
            var next = await capture.CaptureAsync(0, []);
            Check("主动取消真正废弃捕获且后续恢复", canceled && next.HasTarget);
            if (host != null) await host.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            Check("40 秒 verb 完成后断开宿主自行退出", host?.HasExited == true);
        }
        catch (Exception ex) { Check("健壮性检查异常", false, ex.ToString()); }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { Version = AppInfo.Version, Time = DateTimeOffset.Now, Passed = passed, Results = results }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
