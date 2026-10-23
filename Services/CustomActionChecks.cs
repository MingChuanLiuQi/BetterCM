using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Views;
namespace BetterCM.Services;

internal static class CustomActionChecks
{
    internal static async Task RunAsync(Action<CheckIdentity, bool, string?> check, string fixture)
    {
        var app = (App)Application.Current;
        string baseline = ConfigJson.Serialize(app.Config);
        string root = Path.Combine(fixture, "custom-action-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        string first = Path.Combine(root, "报告 {dir} & 空格.txt"), second = Path.Combine(root, "第二个 文件.txt");
        await File.WriteAllTextAsync(first, "first"); await File.WriteAllTextAsync(second, "second");
        var context = new SelectedContext { CurrentDirectory = root, Items = [new(first, false), new(second, false)] };
        var action = new CustomActionItem { Title = "保持原动作", Executable = Environment.ProcessPath!, Arguments = "{paths}", SelectionMode = "batch" };
        var editor = new CustomActionWindow(action, context) { ShowActivated = false };
        var settings = new MainWindow(app) { ShowActivated = false };
        settings.Closing += (_, e) => e.Cancel = false;
        try
        {
            editor.Show(); editor.SelectPreview(3);
            check(new("CustomAction/RecentPreview", "参数预览可使用最近选区且保留花括号文件名"),
                editor.PreviewText.Contains(first, StringComparison.Ordinal) && editor.PreviewText.Contains(second, StringComparison.Ordinal) && editor.PreviewText.Contains("预计启动：1 次", StringComparison.Ordinal), null);
            editor.ApplyTemplate("notepad"); editor.SelectPreview(1);
            check(new("CustomAction/EachTemplate", "记事本模板预览逐个启动且保留原动作草稿身份"),
                editor.PreviewText.Contains("预计启动：2 次", StringComparison.Ordinal) && editor.Result.Id == action.Id && editor.Result.Title == action.Title, null);
            editor.ApplyTemplate("code"); editor.ApplySelectedExecutable(Environment.ProcessPath!); editor.SelectPreview(1);
            check(new("CustomAction/BatchTemplate", "批量模板将两个路径展示为两项独立参数"),
                editor.SelectedTemplateTitle == CustomActionTemplates.All.Single(x => x.Id == "code").Title && editor.PreviewText.Contains("预计启动：1 次", StringComparison.Ordinal) && editor.PreviewText.Contains("[2]", StringComparison.Ordinal) && !editor.PreviewText.Contains("[3]", StringComparison.Ordinal), null);
            await editor.Dispatcher.InvokeAsync(editor.UpdateLayout, System.Windows.Threading.DispatcherPriority.ContextIdle);
            Capture(editor, Path.Combine(root, "editor-preview.png"));
            var bounds = SettingsWindowSizing.Fit(960, 520, editor.Width, editor.Height, editor.MinWidth, editor.MinHeight);
            editor.MinWidth = bounds.MinWidth; editor.MinHeight = bounds.MinHeight; editor.Width = bounds.Width; editor.Height = bounds.Height;
            await editor.Dispatcher.InvokeAsync(editor.UpdateLayout, System.Windows.Threading.DispatcherPriority.ContextIdle);
            var footer = ((Grid)editor.Content).Children.OfType<StackPanel>().Single();
            var position = footer.TranslatePoint(new Point(0, 0), editor);
            check(new("CustomAction/EditorSmallWorkArea", "预览编辑器在较低工作区下保留可达的应用按钮"),
                position.Y >= 0 && position.Y + footer.ActualHeight <= editor.ActualHeight, null);
            var incoming = new AppConfig { CustomActions = [action] }.Clone().CustomActions;
            incoming[0].Title = "导入替换";
            var confirmation = new CustomActionImportWindow([action], incoming) { ShowActivated = false };
            try
            {
                confirmation.Show();
                await confirmation.Dispatcher.InvokeAsync(confirmation.UpdateLayout, System.Windows.Threading.DispatcherPriority.ContextIdle);
                Capture(confirmation, Path.Combine(root, "import-review.png"));
                bool skip = confirmation.Result is { Skipped: 1, Added: 0 };
                confirmation.SelectPolicy(CustomActionDuplicatePolicy.Replace);
                bool replace = confirmation.Result is { Replaced: 1 } && confirmation.Result.Actions[0].Id == action.Id;
                confirmation.SelectPolicy(CustomActionDuplicatePolicy.KeepBoth);
                check(new("CustomAction/ImportPolicies", "导入确认可切换跳过替换及保留两份"), skip && replace && confirmation.Result is { Added: 1 } && confirmation.Result.Actions.Select(x => x.Id).Distinct().Count() == 2, null);
            }
            finally { confirmation.Close(); }
            settings.Show();
            var classic = (CheckBox)settings.FindName("ClassicOption"); classic.IsChecked = true;
            check(new("CustomAction/ClassicTransfer", "经典模式允许导入导出而不会开启动作编辑或切换模式"),
                ((Button)settings.FindName("ImportCustomButton")).IsEnabled && ((Button)settings.FindName("ExportCustomButton")).IsEnabled && !((StackPanel)settings.FindName("CustomCommands")).IsEnabled, null);
            var merged = CustomActionTransfer.Merge(settings.ReadSettings().CustomActions, [action], CustomActionDuplicatePolicy.KeepBoth);
            settings.ApplyCustomImport(merged);
            bool draft = settings.ReadSettings().CustomActions.Count == merged.Actions.Count && settings.HasUnsavedChanges && settings.ReadSettings().ClassicMenu;
            ((Button)settings.FindName("RestoreButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(new("CustomAction/ImportDraft", "导入仅更新待保存列表且可撤销，不改变已保存配置"),
                draft && ConfigJson.Serialize(app.Config) == baseline && settings.ReadSettings().CustomActions.Count == app.Config.CustomActions.Count, null);
            using var bridge = new ShellBridge(); var executor = new ActionExecutor(bridge);
            foreach (var scenario in new[] { (Mode: "single", Target: false), (Mode: "each", Target: true), (Mode: "batch", Target: false), (Mode: "batch", Target: true) })
            {
                var selected = scenario.Mode == "single" ? context with { Items = [context.Items[0]] } : context;
                string output = Path.Combine(root, scenario.Mode + "-" + scenario.Target + "-{name}.json");
                var probe = new CustomActionItem
                {
                    Title = "受控启动探测", Executable = Environment.ProcessPath!, SelectionMode = scenario.Mode,
                    UseTargetWorkingDirectory = scenario.Target,
                    Arguments = "--verb-probe \"" + output + "\" " + (scenario.Mode == "batch" ? "{paths}" : "\"{path}\" \"{dir}\" \"{name}\"")
                };
                var plan = new CustomActionPlan(probe, selected);
                await executor.ExecuteCustomAsync(probe, selected);
                bool matches = true;
                for (int i = 0; i < plan.Count; i++)
                {
                    var launch = plan.Launch(i);
                    string? report = await ShellLaunchChecks.ReadProbeAsync(launch.Arguments[1], TimeSpan.FromSeconds(5));
                    if (report == null) { matches = false; continue; }
                    using var json = JsonDocument.Parse(report);
                    matches &= json.RootElement.GetProperty("Arguments").EnumerateArray().Select(x => x.GetString()).SequenceEqual(launch.Arguments.Skip(2)) &&
                        json.RootElement.GetProperty("WorkingDirectory").GetString() == launch.WorkingDirectory;
                }
                check(new("CustomAction/Launch/" + scenario.Mode + "/" + scenario.Target, "真实自定义启动与预览计划一致：" + scenario.Mode + "/" + scenario.Target), matches, null);
            }
        }
        finally { settings.Close(); editor.Close(); }
    }
    private static void Capture(Window window, string destination)
    {
        int width = (int)Math.Ceiling(window.ActualWidth), height = (int)Math.Ceiling(window.ActualHeight);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(Application.Current.Resources["MenuBg"] as Brush ?? Brushes.White, null, new Rect(0, 0, width, height));
            drawing.DrawRectangle(new VisualBrush(window), null, new Rect(0, 0, width, height));
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(destination); encoder.Save(file);
    }
}
