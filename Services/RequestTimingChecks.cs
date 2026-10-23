using System.Windows;
using System.Diagnostics;
using System.Reflection;
using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Views;
namespace BetterCM.Services;

internal static class RequestTimingChecks
{
    internal static async Task RunAsync(Action<CheckIdentity, bool, string?> check, string fixture)
    {
        var menu = new ContextMenuWindow { Host = (App)Application.Current, ShowActivated = false };
        var context = new SelectedContext { CurrentDirectory = fixture, Source = "请求计时回归" };
        MenuPresentationRequest Request(AppConfig config, MenuRequestTiming timing, Action<bool, bool> complete) => new()
        {
            X = 24, Y = 24, Context = context, Config = config, Activate = false,
            Primary = [new() { Id = "timing", Title = "请求计时", Execute = () => Task.CompletedTask }],
            WindowShown = timing.WindowShown, PresentationObserved = complete
        };
        try
        {
            foreach (bool animated in new[] { false, true })
            {
                var timing = new MenuRequestTiming(new(0, 0, 0, true));
                var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                int callbacks = 0;
                menu.ShowAt(Request(new() { EnableAnimations = animated }, timing, (rendered, keyboard) =>
                {
                    callbacks++; timing.Complete(rendered, keyboard); done.TrySetResult();
                }));
                await done.Task.WaitAsync(TimeSpan.FromSeconds(3));
                await Task.Delay(25);
                var sample = timing.Sample;
                check(new("RequestTiming/Rendered/" + animated, "呈现计时覆盖有无动画且不伪报键盘焦点：" + animated),
                    callbacks == 1 && sample.WindowShownMs != null && sample.PresentationObservedMs >= sample.WindowShownMs &&
                    sample.Outcome == "FocusUnavailable" && !sample.KeyboardReady, System.Text.Json.JsonSerializer.Serialize(sample));
                menu.CloseMenu("下一采样", true);
            }
            var cancelled = new MenuRequestTiming(new(0, 0, 0));
            int cancelCallbacks = 0;
            menu.ShowAt(Request(new() { EnableAnimations = false }, cancelled, (rendered, keyboard) => { cancelCallbacks++; cancelled.Complete(rendered, keyboard); }));
            menu.CloseMenu("立即取消", true);
            var replacement = new MenuRequestTiming(new(0, 0, 0));
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            menu.ShowAt(Request(new() { EnableAnimations = false }, replacement, (rendered, keyboard) => { replacement.Complete(rendered, keyboard); ready.TrySetResult(); }));
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(3));
            check(new("RequestTiming/CancelledGeneration", "取消菜单仅回调一次且不把后续呈现计入旧请求"),
                cancelCallbacks == 1 && cancelled.Sample.Outcome == "DismissedBeforePresentation" && cancelled.Sample.PresentationObservedMs == null &&
                replacement.Sample.PresentationObservedMs != null, null);
            menu.CloseMenu("应用请求回归", true);
            var app = (App)Application.Current;
            var fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var menuField = typeof(App).GetField("_menu", fields)!;
            var requestField = typeof(App).GetField("_menuRequest", fields)!;
            var trigger = typeof(App).GetMethod("TriggerAsync", fields)!;
            object? oldMenu = menuField.GetValue(app);
            menuField.SetValue(app, menu);
            try
            {
                var desktop = NativeMethods.FindWindow("Progman", null);
                var pending = (Task)trigger.Invoke(app, [new TriggerRequest(desktop, 24, 24, ReceivedTimestamp: Stopwatch.GetTimestamp())])!;
                app.CancelMenuPreparation(-(int)requestField.GetValue(app)!);
                await pending;
                var stopped = app.RecentMenuRequests.Last();
                check(new("RequestTiming/CancelCapture", "上下文捕获期间取消请求后不会迟到弹出菜单"),
                    !menu.IsOpen && stopped.Outcome == "Cancelled" && stopped.WindowShownMs == null, System.Text.Json.JsonSerializer.Serialize(stopped));

                // Exercise the application's real capture/Shell/composition pipeline with a synthetic accepted request.
                // This is not a physical mouse/hotkey injection or a hardware latency benchmark.
                await (Task)trigger.Invoke(app, [new TriggerRequest(desktop, 24, 24, ReceivedTimestamp: Stopwatch.GetTimestamp())])!;
                var timeout = Stopwatch.StartNew();
                while (app.RecentMenuRequests.Last().RequestId == stopped.RequestId && timeout.Elapsed < TimeSpan.FromSeconds(3)) await Task.Delay(5);
                var completed = app.RecentMenuRequests.Last();
                check(new("RequestTiming/AppPipeline", "真实应用编排记录上下文至呈现的全部累计阶段"),
                    completed.RequestId != stopped.RequestId && completed.ContextReadyMs >= completed.QueueMs &&
                    completed.ShellReadyMs >= completed.ContextReadyMs && completed.ActionsReadyMs >= completed.ShellReadyMs &&
                    completed.WindowShownMs >= completed.ActionsReadyMs && completed.PresentationObservedMs >= completed.WindowShownMs &&
                    completed.Outcome is "Ready" or "FocusUnavailable", System.Text.Json.JsonSerializer.Serialize(completed));
            }
            finally { menu.CloseMenu("应用请求回归结束", true); menuField.SetValue(app, oldMenu); }
        }
        finally { menu.CloseMenu("请求计时回归结束", true); menu.Close(); }
    }
}
