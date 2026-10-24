using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Views;
namespace BetterCM.Services;

internal static class Iteration210Checks
{
    internal static async Task RunAsync(Action<CheckIdentity, bool, string?> check, string fixture)
    {
        var app = (App)Application.Current; string baseline = ConfigJson.Snapshot(app.Config);
        string root = Path.Combine(fixture, "iteration210-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        string file = Path.Combine(root, "报告 {path}.txt"); await File.WriteAllTextAsync(file, "input fixture");
        var context = new SelectedContext { Items = [new(file, false)], CurrentDirectory = root };
        var recent = new SelectedContext { Items = [new(@"C:\First\one.txt", false)] };
        var editor = new MenuWorkspaceEditor();
        editor.Load(new() { AutoSelectProfile = true, MenuProfiles = [new() { Title = "第一方案", DirectoryRoot = @"C:\First" }, new() { Title = "第二方案", DirectoryRoot = @"C:\Second" }] }, () => [], recent, () => recent);
        recent = new() { Items = [new(@"C:\Second\second.txt", false)] };
        check(new("Iteration210/RecentContext", "最近选区测试读取更新后的上下文且保留草稿"), editor.TestRecentSelection().Contains("第二方案", StringComparison.Ordinal) && editor.TestRecentSelection().Contains("second.txt", StringComparison.Ordinal), null);
        var action = new CustomActionItem
        {
            Title = "运行参数探针", Executable = Environment.ProcessPath!, Arguments = "--verb-probe \"" + Path.Combine(root, "probe.json") + "\" \"{input:label}\" {input:width} {input:mode} \"{path}\"",
            Parameters = [new() { Id = "label", Title = "名称", DefaultValue = "draft" }, new() { Id = "width", Title = "宽度", Kind = "number", Minimum = 1, Maximum = 10, DefaultValue = "5" }, new() { Id = "mode", Title = "模式", Kind = "choice", DefaultValue = "A", Choices = ["A", "B"] }]
        };
        var settings = new MainWindow(app) { ShowActivated = false }; settings.Closing += (_, e) => e.Cancel = false;
        var rename = new BatchRenameWindow(context) { ShowActivated = false };
        var review = new WorkspaceImportWindow(settings.ReadSettings(), new() { Theme = "light", CustomActions = [action], MenuProfiles = [new() { Id = "imported", Title = "导入方案", ActionIds = [action.Id] }], ActiveProfileId = "imported", RenamePresets = [new() { Title = "归档", Options = new() { SortOrder = "name", Numbered = true } }] }) { ShowActivated = false };
        try
        {
            review.Show(); review.SelectRestore(true); await review.Dispatcher.InvokeAsync(review.UpdateLayout, System.Windows.Threading.DispatcherPriority.ContextIdle);
            check(new("Iteration210/BackupReview", "工作区恢复预览包含运行参数方案引用和重命名预设"), review.Result?.Configuration.CustomActions[0].Parameters.Count == 3 && review.Result.Configuration.MenuProfiles[0].ActionIds[0] == action.Id && review.Result.Configuration.RenamePresets.Count == 1, null);
            CustomActionChecks.Capture(review, Path.Combine(root, "workspace-review.png"));
            settings.ApplyWorkspaceImport(review.Result!.Configuration, baseline);
            var draft = settings.ReadSettings();
            check(new("Iteration210/BackupDraft", "完整恢复只更新设置草稿且可还原"), draft.Theme == "light" && draft.CustomActions.Single().Parameters.Count == 3 && draft.RenamePresets.Count == 1 && baseline == ConfigJson.Snapshot(app.Config), null);
            bool conflict = false; try { settings.ApplyWorkspaceImport(draft, "outdated-baseline"); } catch (IOException) { conflict = true; }
            settings.RefreshSettings(); check(new("Iteration210/BackupConflict", "导入确认期间配置变化会拒绝覆盖并保护当前设置"), conflict && baseline == ConfigJson.Snapshot(app.Config), null);
            rename.Show(); rename.ApplyOptions(new() { SortOrder = "name", Numbered = true, Suffix = "-", TrimSpaces = true });
            var presetChoice = Descendants<ComboBox>(rename).Single(x => System.Windows.Automation.AutomationProperties.GetName(x) == "重命名预设");
            check(new("Iteration210/PresetOptions", "重命名窗口可应用并读取预设的排序编号与清理规则"), rename.ReadOptions().SortOrder == "name" && rename.ReadOptions().Numbered && rename.ReadOptions().TrimSpaces && (presetChoice.SelectedItem as ComboBoxItem)?.Content as string == RenamePresets.Builtins[0].Title, null);
            await rename.PreviewAsync(); await rename.Dispatcher.InvokeAsync(rename.UpdateLayout, System.Windows.Threading.DispatcherPriority.ContextIdle);
            CustomActionChecks.Capture(rename, Path.Combine(root, "rename-presets.png"));
            rename.ApplyOptions(new() { Prefix = "?" }); await rename.PreviewAsync();
            var invalid = Descendants<DataGrid>(rename).Single().Items.Cast<RenamePreviewRow>().Select(x => x.Plan).Single();
            check(new("Iteration210/InvalidNamePreview", "无效重命名预览显示拟生成的错误名称"), invalid.NewName.StartsWith('?') && invalid.Error.Length > 0, null);
            string payload = "空格 & {path} \"引号\""; bool invalidNumberBlocked = false;
            var input = new RuntimeInputWindow(action, context) { ShowActivated = false };
            input.Loaded += (_, _) => input.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    var boxes = Descendants<TextBox>(input).Where(x => !x.IsReadOnly).ToArray();
                    var number = boxes.Single(x => System.Windows.Automation.AutomationProperties.GetName(x) == "宽度");
                    var run = Descendants<Button>(input).Single(x => (string?)x.Content == "按此预览运行");
                    number.Text = "11"; invalidNumberBlocked = !run.IsEnabled; number.Text = "7";
                    boxes.Single(x => System.Windows.Automation.AutomationProperties.GetName(x) == "名称").Text = payload;
                    Descendants<ComboBox>(input).Single().SelectedItem = "B";
                    input.UpdateLayout(); CustomActionChecks.Capture(input, Path.Combine(root, "runtime-input.png"));
                    run.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                catch { input.Close(); throw; }
            }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            bool accepted = input.ShowDialog() == true;
            check(new("Iteration210/RuntimeForm", "运行参数表单阻止越界整数并保留文本和下拉选择"), accepted && invalidNumberBlocked && input.Values?["label"] == payload && input.Values["width"] == "7" && input.Values["mode"] == "B", null);
            using var shell = new ShellBridge(); await new ActionExecutor(shell).ExecuteCustomAsync(action, context, inputs: input.Values);
            string report = Path.Combine(root, "probe.json");
            for (int i = 0; i < 100 && !File.Exists(report); i++) await Task.Delay(25);
            using var probe = JsonDocument.Parse(await File.ReadAllTextAsync(report));
            var arguments = probe.RootElement.GetProperty("Arguments").EnumerateArray().Select(x => x.GetString()).ToArray();
            check(new("Iteration210/RuntimeLaunch", "真实进程收到的运行参数与表单预览一致且不二次展开"), arguments.SequenceEqual(new[] { payload, "7", "B", file }), null);
            using var cancellation = new CancellationTokenSource(); bool ready = false, cancelled = false;
            using (var fake = new ShellBridge(Timeout.InfiniteTimeSpan, testHost: true))
            {
                cancellation.CancelAfter(150); var clock = Stopwatch.StartNew();
                try { await fake.TransferAsync(context, new() { Path = root }, false, cancellation.Token, () => ready = true); } catch (OperationCanceledException) { cancelled = true; }
                check(new("Iteration210/PrepareCancel", "传输准备等待可取消且不会提交原生执行"), cancelled && !ready && clock.Elapsed < TimeSpan.FromSeconds(3), null);
            }
            string destination = Path.Combine(root, "取消准备"); Directory.CreateDirectory(destination);
            using var beforeCommit = new CancellationTokenSource(); bool realCancelled = false;
            try { await FavoriteTransfer.TransferAsync(context, new() { Path = destination, DateSubfolder = true }, false, beforeCommit.Token, beforeCommit.Cancel); } catch (OperationCanceledException) { realCancelled = true; }
            check(new("Iteration210/CancelBeforeCommit", "真实目标准备后取消不会创建日期目录或传输文件"), realCancelled && Directory.GetFileSystemEntries(destination).Length == 0 && File.Exists(file), null);
        }
        finally { review.Close(); rename.Close(); settings.Close(); }
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); if (child is T match) yield return match;
            foreach (var item in Descendants<T>(child)) yield return item;
        }
    }
}
