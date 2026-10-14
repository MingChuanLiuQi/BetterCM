using System.Security.Cryptography;
using System.Text.Json;
using BetterCM.Config;
using BetterCM.Models;
namespace BetterCM.Services;

public sealed record RegressionCheck(string Name, bool Passed, string? Detail = null);
public sealed record RegressionSelection(SelectedContext Single, SelectedContext Multiple);
public static class RegressionFixtures
{
    public static async Task<RegressionSelection> CreateAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var text = Path.Combine(directory, "空格 & 中文.txt");
        var other = Path.Combine(directory, "second.json");
        await File.WriteAllTextAsync(text, AppInfo.DisplayName + " regression fixture.\n");
        await File.WriteAllTextAsync(other, "{}");
        return new(new() { Source = "测试", CurrentDirectory = directory, Items = [new(text, false)] }, new() { CurrentDirectory = directory, Items = [new(text, false), new(other, false)] });
    }
}

// Shared contract checks do no window creation, Shell COM, input, or system clipboard writes.
public static class LogicChecks
{
    public static IReadOnlyList<string> Names { get; } = [
        "Alt+X 热键解析",
        "Ctrl+Shift+F12 热键解析",
        "拒绝无修饰键",
        "拒绝无效按键",
        "WSL 路径转换",
        "WSL 驱动器根目录",
        "UNC 不假装是 WSL 驱动器",
        "参数替换保留空格与特殊字符",
        "参数先分词再替换",
        "多选复制保留全部路径",
        "多选不显示单文件哈希",
        "单选文本可编辑",
        "目录不显示记事本",
        "无目标不显示文件动作",
        "混合选区扩展名全部匹配",
        "逐个模式支持混合多选",
        "单选模式拒绝多选",
        "配置保存成功",
        "配置可重新读取",
        "错误 JSON 不覆盖原文件",
        "错误 JSON 保留最后有效配置",
        "无效主题校验",
        "新配置关闭动画默认 70 ms",
        "batch 没有 paths 参数被拒绝",
        "batch 嵌入 paths 参数被拒绝",
        "batch 独立 paths 参数可保存",
        "虚拟文件夹保留真实 Shell 目标",
        "虚拟文件夹不执行文件路径动作",
        "导航栏固定标记不改变目标名称",
        "物理背景快照跨进程序列化不会递归且保留目标",
        "0.2 配置保留偏好并获得新字段默认值",
        "交互和动效配置完整保存与重载",
        "拒绝无效材质",
        "拒绝过短的悬停延时",
        "拒绝过长的悬停延时",
        "拒绝未知打开动画",
        "拒绝未知关闭动画",
        "拒绝过短的动画时长",
        "拒绝过长的动画时长",
        "拒绝越界位移",
        "拒绝越界缩放",
        "拒绝未知动画曲线",
        "无效自定义多选模式校验",
        "异步 SHA256 与标准结果一致",
        "哈希可取消",
        "未展开菜单只呈现一级动作",
        "压缩工具合并为单个入口",
        "不同格式路径合并入口",
        "分组入口显示动作总数",
        "相同工具的 Git 入口合并",
        "不同应用的打开动作合并",
        "所有分组只呈现各自一级项目",
        "嵌套分组保留原始命令身份",
        "悬停展开后常用压缩动作直接呈现",
        "校验值仍合并于独立用途入口",
        "邮件压缩合并于独立用途入口",
        "完全展开不丢失任何可执行动作",
        "完全展开保持唯一命令身份",
        "关闭分组也能搜索深层命令",
        "搜索来源可以定位扩展的全部动作",
        "固定项目保留直接入口",
        "固定项目不在分组重复出现",
        "规范化原生助记键后缀",
        "保留具有含义的括号内容",
        "合并已启用的重复终端动作",
        "禁用内建动作时保留原生替代项",
        "第三方分组的同名命令不误合并",
        "菜单内部点击不请求关闭",
        "外部及右下边界点击请求关闭",
        "左中右及附加键与外部滚轮均可关闭",
        "单纯移出菜单不会关闭",
        "旧关闭消息不影响新菜单",
        "隐藏菜单忽略延迟关闭消息",
        "菜单显示前的前台事件不关闭新菜单",
        "事件时间比较支持系统计时回绕",
        "焦点重试允许原来源窗口",
        "焦点重试允许当前菜单",
        "焦点重试拒绝其他应用和无来源",
        "刷新默认位于一级且只支持背景视图",
        "虚拟背景刷新不依赖物理路径",
        "合并原生刷新避免重复入口",
        "拒绝重复快捷键修饰键",
        "拒绝空快捷键段",
        "旧配置获得展开默认值",
        "展开配置完整保存与重载",
        "拒绝无效更多展开样式",
        "拒绝无效分组展开样式",
        "拒绝过短展开时长",
        "拒绝过长分组时长",
        "展开曲线单调且端点正确",
        "接管手势成对结束且保留原始目标",
        "弹出菜单右键成对吞掉不创建请求",
        "原生手势不触发接管",
        "重复松开不重触发",
        "连续一万次手势不丢失配对",
        "内置项目与原生来源可靠区分",
        "扩展组来源不改变原始命令身份",
    ];
    public static async Task<IReadOnlyList<RegressionCheck>> RunAsync(string fixture)
    {
        Directory.CreateDirectory(fixture);
        var results = new List<RegressionCheck>();
        void Check(string name, bool passed, string? detail = null) => results.Add(new(name, passed, detail));
        void Reject(string name, Action action) { try { action(); Check(name, false, "应拒绝"); } catch { Check(name, true); } }
        try
        {
            Reject("拒绝重复快捷键修饰键", () => Hotkey.Parse("Ctrl+Control+X"));
            Reject("拒绝空快捷键段", () => Hotkey.Parse("Alt++X"));
            var legacyExpansion = JsonSerializer.Deserialize<AppConfig>("{\"OpenAnimationMs\":200}")!;
            Check("旧配置获得展开默认值", legacyExpansion.EnableExpansionAnimations && legacyExpansion.MoreExpansionMs == 220 && legacyExpansion.GroupExpansionMs == 180);
            var expansionConfig = new AppConfig { EnableExpansionAnimations = false, MoreExpansionAnimation = "slide", GroupExpansionAnimation = "fade", MoreExpansionMs = 300, GroupExpansionMs = 90 };
            var expansionStore = new ConfigStore(Path.Combine(fixture, "expansion-config.json"));
            Check("展开配置完整保存与重载", expansionStore.Save(expansionConfig) && expansionStore.Load() && JsonSerializer.Serialize(expansionStore.Current) == JsonSerializer.Serialize(expansionConfig));
            Reject("拒绝无效更多展开样式", () => ConfigStore.Validate(new() { MoreExpansionAnimation = "invalid" }));
            Reject("拒绝无效分组展开样式", () => ConfigStore.Validate(new() { GroupExpansionAnimation = "zoom" }));
            Reject("拒绝过短展开时长", () => ConfigStore.Validate(new() { MoreExpansionMs = 0 }));
            Reject("拒绝过长分组时长", () => ConfigStore.Validate(new() { GroupExpansionMs = 401 }));
            Check("展开曲线单调且端点正确", new[] { "cubic", "quintic", "sine", "linear" }.All(easing => ExpansionAnimator.Ease(0, easing) == 0 && ExpansionAnimator.Ease(1, easing) == 1 && Enumerable.Range(0, 100).All(i => ExpansionAnimator.Ease(i / 100.0, easing) <= ExpansionAnimator.Ease((i + 1) / 100.0, easing))));
            var gesture = new RightClickGesture(); var originRequest = new TriggerRequest(42, -150, 320, true, true);
            gesture.Begin(true, originRequest);
            Check("接管手势成对结束且保留原始目标", gesture.Owned && gesture.End() == originRequest && !gesture.Owned);
            gesture.Begin(true);
            Check("弹出菜单右键成对吞掉不创建请求", gesture.Owned && gesture.End() == null && !gesture.Owned);
            gesture.Begin(false, originRequest);
            Check("原生手势不触发接管", !gesture.Owned && gesture.End() == null);
            gesture.Begin(true, originRequest); gesture.End();
            Check("重复松开不重触发", gesture.End() == null && !gesture.Owned);
            bool paired = true;
            for (int i = 0; i < 10000; i++) { var request = originRequest with { X = i }; gesture.Begin(true, request); paired &= gesture.Owned && gesture.End() == request && !gesture.Owned; }
            Check("连续一万次手势不丢失配对", paired);
            List<MenuAction> providerActions = [new() { Id = "shell-root", ShellKey = "root", Title = "打印" }, new() { Id = "shell-group", ShellKey = "group", Group = "NanaZip", Title = "添加到压缩包" }];
            var providerRows = MenuComposer.Compose([new() { Id = "builtin", Title = "打开" }], [], providerActions, new HashSet<string>(), true, "");
            Check("内置项目与原生来源可靠区分", !providerRows.Single(x => x.Id == "builtin").IsShell && providerRows.Single(x => x.Id == "shell-root").Source == "Shell");
            var providerLeaf = providerRows.Single(x => x.IsGroup).Children.Single();
            Check("扩展组来源不改变原始命令身份", providerLeaf.Source == "NanaZip" && providerLeaf.Action == providerActions[1] && providerRows.Any(x => x.IsGroup && x.IsShell));
            Check("Alt+X 热键解析", Hotkey.Parse("Alt+X") == new Hotkey(0x4001, 'X'));
            Check("Ctrl+Shift+F12 热键解析", Hotkey.Parse("Ctrl+Shift+F12") == new Hotkey(0x4006, 123));
            Reject("拒绝无修饰键", () => Hotkey.Parse("X"));
            Reject("拒绝无效按键", () => Hotkey.Parse("Alt+Invalid"));
            Check("WSL 路径转换", PathFormatter.ToWsl(@"C:\Work Space\hello.txt") == "/mnt/c/Work Space/hello.txt");
            Check("WSL 驱动器根目录", PathFormatter.ToWsl(@"D:\") == "/mnt/d/");
            Reject("UNC 不假装是 WSL 驱动器", () => PathFormatter.ToWsl(@"\\server\share\file.txt"));
            Check("参数替换保留空格与特殊字符", PathFormatter.Expand("{path}", @"C:\A&B\test space.txt", @"C:\A&B") == @"C:\A&B\test space.txt");
            var args = ActionExecutor.ParseArguments("\"{path}\" --name \"{name}\" \"{paths}\"");
            Check("参数先分词再替换", args.SequenceEqual(new[] { "{path}", "--name", "{name}", "{paths}" }));
            var selections = await RegressionFixtures.CreateAsync(fixture);
            var textPath = selections.Single.PrimaryPath; var single = selections.Single; var multiple = selections.Multiple;
            Check("多选复制保留全部路径", multiple.TargetPaths.Count() == 2);
            Check("多选不显示单文件哈希", !ActionCatalog.All.First(x => x.Id == "hash").Supports(multiple));
            Check("单选文本可编辑", ActionCatalog.All.First(x => x.Id == "notepad").Supports(single));
            Check("目录不显示记事本", !ActionCatalog.All.First(x => x.Id == "notepad").Supports(new() { CurrentDirectory = fixture }));
            Check("无目标不显示文件动作", ActionCatalog.All.All(x => !x.Supports(new())));
            var custom = new CustomActionItem { Title = "test", Executable = "test", MatchExtensions = [".txt"], SelectionMode = "each" };
            Check("混合选区扩展名全部匹配", !ActionCatalog.Matches(custom, multiple));
            custom.MatchExtensions = [".txt", ".json"];
            Check("逐个模式支持混合多选", ActionCatalog.Matches(custom, multiple));
            custom.SelectionMode = "single";
            Check("单选模式拒绝多选", !ActionCatalog.Matches(custom, multiple));
            var configPath = Path.Combine(fixture, "config-test.json"); var store = new ConfigStore(configPath);
            Check("配置保存成功", store.Save(new() { GlobalHotkey = "Ctrl+Alt+M" }));
            Check("配置可重新读取", store.Load() && store.Current.GlobalHotkey == "Ctrl+Alt+M");
            await File.WriteAllTextAsync(configPath, "{ invalid");
            Check("错误 JSON 不覆盖原文件", !store.Load() && File.ReadAllText(configPath) == "{ invalid");
            Check("错误 JSON 保留最后有效配置", store.Current.GlobalHotkey == "Ctrl+Alt+M");
            Reject("无效主题校验", () => ConfigStore.Validate(new() { Theme = "invalid" }));
            Check("新配置关闭动画默认 70 ms", new AppConfig().CloseAnimationMs == 70);
            Reject("batch 没有 paths 参数被拒绝", () => ConfigStore.Validate(new() { CustomActions = [new() { Title = "batch", Executable = "tool", SelectionMode = "batch" }] }));
            Reject("batch 嵌入 paths 参数被拒绝", () => ConfigStore.Validate(new() { CustomActions = [new() { Title = "batch", Executable = "tool", SelectionMode = "batch", Arguments = "--files={paths}" }] }));
            ConfigStore.Validate(new() { CustomActions = [new() { Title = "batch", Executable = "tool", SelectionMode = "batch", Arguments = "\"{paths}\"" }] });
            Check("batch 独立 paths 参数可保存", true);
            var virtualContext = ExplorerContextService.Build([], "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", "此电脑", "测试", true);
            Check("虚拟文件夹保留真实 Shell 目标", virtualContext.HasTarget && !virtualContext.HasFileTarget && virtualContext.PrimaryName == "此电脑" && virtualContext.ShellBackground);
            Check("虚拟文件夹不执行文件路径动作", ActionCatalog.All.Where(x => !ShellCommandMap.IsNative(x.Id)).All(x => !x.Supports(virtualContext)) && !ActionCatalog.Matches(custom, virtualContext));
            Check("导航栏固定标记不改变目标名称", ExplorerContextService.NavigationName("下载 (已固定)") == "下载" && ExplorerContextService.NavigationName("快速访问开始 - 桌面 (已固定)") == "桌面" && ExplorerContextService.NavigationName("Quick access end - Videos (pinned)") == "Videos");
            var backgroundContext = ExplorerContextService.Build([], fixture, "夹具", "测试", true);
            var roundTrip = JsonSerializer.Deserialize<SelectedContext>(JsonSerializer.Serialize(backgroundContext))!;
            Check("物理背景快照跨进程序列化不会递归且保留目标", roundTrip.HasFileTarget && roundTrip.ShellTargets.SequenceEqual([fixture]));
            var upgraded = JsonSerializer.Deserialize<AppConfig>("{\"Theme\":\"light\",\"EnableAnimations\":false}")!;
            Check("0.2 配置保留偏好并获得新字段默认值", upgraded.Theme == "light" && !upgraded.EnableAnimations && upgraded.Material == "mica" && upgraded.ShowMoreItems && upgraded.MoreHoverDelayMs == 180);
            var visualConfig = new AppConfig { Material = "acrylic", ShowMoreItems = false, MoreHoverDelayMs = 300, OpenAnimation = "zoom", CloseAnimation = "slide", OpenAnimationMs = 200, CloseAnimationMs = 90, AnimationDistance = 24, AnimationScalePercent = 92, AnimationEasing = "sine" };
            Check("交互和动效配置完整保存与重载", store.Save(visualConfig) && store.Load() && JsonSerializer.Serialize(store.Current) == JsonSerializer.Serialize(visualConfig));
            Reject("拒绝无效材质", () => ConfigStore.Validate(new() { Material = "invalid" }));
            Reject("拒绝过短的悬停延时", () => ConfigStore.Validate(new() { MoreHoverDelayMs = 0 }));
            Reject("拒绝过长的悬停延时", () => ConfigStore.Validate(new() { MoreHoverDelayMs = 601 }));
            Reject("拒绝未知打开动画", () => ConfigStore.Validate(new() { OpenAnimation = "invalid" }));
            Reject("拒绝未知关闭动画", () => ConfigStore.Validate(new() { CloseAnimation = "invalid" }));
            Reject("拒绝过短的动画时长", () => ConfigStore.Validate(new() { OpenAnimationMs = 1 }));
            Reject("拒绝过长的动画时长", () => ConfigStore.Validate(new() { CloseAnimationMs = 401 }));
            Reject("拒绝越界位移", () => ConfigStore.Validate(new() { AnimationDistance = 33 }));
            Reject("拒绝越界缩放", () => ConfigStore.Validate(new() { AnimationScalePercent = 100 }));
            Reject("拒绝未知动画曲线", () => ConfigStore.Validate(new() { AnimationEasing = "invalid" }));
            Reject("无效自定义多选模式校验", () => ConfigStore.Validate(new() { CustomActions = [new() { Title = "t", Executable = "t", SelectionMode = "invalid" }] }));
            var hashPath = Path.Combine(fixture, "hash-fixture.bin");
            var bytes = Enumerable.Range(0, 4 * 1024 * 1024).Select(x => (byte)(x % 251)).ToArray();
            await File.WriteAllBytesAsync(hashPath, bytes);
            Check("异步 SHA256 与标准结果一致", await ActionExecutor.HashAsync(hashPath, CancellationToken.None, null) == Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            try { await ActionExecutor.HashAsync(hashPath, cancellation.Token, null); Check("哈希可取消", false); } catch (OperationCanceledException) { Check("哈希可取消", true); }
            var primary = new List<MenuAction> { new() { Id = "terminal", Title = "在终端中打开" } };
            var more = new List<MenuAction> { new() { Id = "slash", Title = "复制正斜杠路径" }, new() { Id = "wsl", Title = "复制 WSL 路径" } };
            var native = new List<MenuAction> {
                new() { Id = "zip", ShellKey = "NanaZip|zip", Group = "NanaZip", Title = "添加到压缩包…" },
                new() { Id = "7z", ShellKey = "NanaZip|7z", Group = "NanaZip", Title = "压缩成 7z" },
                new() { Id = "mail", ShellKey = "NanaZip|mail", Group = "NanaZip", Title = "压缩并邮寄…" },
                new() { Id = "crc", ShellKey = "NanaZip|crc", Group = "NanaZip", Title = "CRC-32" },
                new() { Id = "sha", ShellKey = "NanaZip|sha", Group = "NanaZip", Title = "SHA-256" },
                new() { Id = "pin", ShellKey = "pin", Title = "固定到快速访问(Q)" },
                new() { Id = "git-gui", ShellKey = "git-gui", Title = "Open Git GUI here" },
                new() { Id = "git-bash", ShellKey = "git-bash", Title = "Open Git Bash here" },
                new() { Id = "vs", ShellKey = "vs", Title = "使用 Visual Studio 打开(V)" },
                new() { Id = "pebrel", ShellKey = "pebrel", Title = "在 Pebrel 中打开" },
                new() { Id = "nested", ShellKey = "other|nested", Group = "其他工具 · 格式", Title = "转换文件" }
            };
            List<MenuEntry> Rows(bool expanded, HashSet<string>? pins = null, string filter = "") =>
                MenuComposer.Compose(primary, more, native, pins ?? [], expanded, filter);
            IEnumerable<MenuEntry> Leaves(IEnumerable<MenuEntry> entries) => entries.SelectMany(x => x.IsGroup ? Leaves(x.Children) : x.IsSeparator ? [] : new[] { x });
            Check("未展开菜单只呈现一级动作", Rows(false).Count == 1);
            var grouped = Rows(true);
            Check("压缩工具合并为单个入口", grouped.Count(x => x.Title == "NanaZip" && x.IsGroup) == 1 && grouped.All(x => x.Action?.Id != "crc"));
            Check("不同格式路径合并入口", grouped.Any(x => x.Title == "复制其他格式路径" && x.IsGroup) && grouped.All(x => x.Action?.Id != "wsl"));
            Check("分组入口显示动作总数", grouped.Single(x => x.Title == "NanaZip").Hint == "5");
            Check("相同工具的 Git 入口合并", grouped.Single(x => x.Title == "Git" && x.IsGroup).Hint == "2");
            Check("不同应用的打开动作合并", grouped.Single(x => x.Title == "打开方式" && x.IsGroup).Hint == "2");
            var zipOpen = grouped.Single(x => x.Title == "NanaZip").Children;
            Check("所有分组只呈现各自一级项目", zipOpen.Any(x => x.IsGroup) && zipOpen.All(x => x.Id != "sha" && x.Id != "mail"));
            Check("嵌套分组保留原始命令身份", zipOpen.Single(x => x.Title == "校验与哈希").Children.Single(x => x.Id == "sha").Action == native.Single(x => x.Id == "sha"));
            Check("悬停展开后常用压缩动作直接呈现", zipOpen.Any(x => x.Action?.Id == "zip"));
            Check("校验值仍合并于独立用途入口", zipOpen.Any(x => x.Title == "校验与哈希" && x.IsGroup) && zipOpen.All(x => x.Action?.Id != "sha"));
            Check("邮件压缩合并于独立用途入口", zipOpen.Any(x => x.Title == "压缩并发送" && x.IsGroup) && zipOpen.All(x => x.Action?.Id != "mail"));
            var allOpen = Leaves(grouped).ToList();
            Check("完全展开不丢失任何可执行动作", allOpen.Count(x => x.Action != null) == primary.Count + more.Count + native.Count);
            Check("完全展开保持唯一命令身份", allOpen.Where(x => x.Action != null).Select(x => x.Action!.Id).Distinct().Count() == primary.Count + more.Count + native.Count);
            Check("关闭分组也能搜索深层命令", Rows(true, filter: "SHA-256").Single().Action?.Id == "sha");
            Check("搜索来源可以定位扩展的全部动作", Rows(true, filter: "NanaZip").Count == 5);
            var pinned = Rows(true, pins: ["NanaZip|sha"]);
            Check("固定项目保留直接入口", pinned.Count(x => x.Action?.Id == "sha" && x.IsPinned) == 1);
            Check("固定项目不在分组重复出现", Leaves(pinned).Count(x => x.Action?.Id == "sha") == 1);
            Check("规范化原生助记键后缀", MenuComposer.Caption("固定到快速访问(Q)") == "固定到快速访问");
            Check("保留具有含义的括号内容", MenuComposer.Caption("打开 (只读)") == "打开 (只读)");
            Check("合并已启用的重复终端动作", MenuComposer.DuplicatesBuiltin(new() { Title = "在终端中打开(T)" }, primary));
            Check("禁用内建动作时保留原生替代项", !MenuComposer.DuplicatesBuiltin(new() { Title = "在终端中打开(T)" }, []));
            Check("第三方分组的同名命令不误合并", !MenuComposer.DuplicatesBuiltin(new() { Title = "在终端中打开", Group = "第三方" }, primary));
            var bounds = new NativeMethods.RECT { Left = 100, Top = 200, Right = 420, Bottom = 600 };
            Check("菜单内部点击不请求关闭", !MenuDismissPolicy.IsOutside(bounds, new() { X = 100, Y = 200 }) && !MenuDismissPolicy.IsOutside(bounds, new() { X = 419, Y = 599 }));
            Check("外部及右下边界点击请求关闭", MenuDismissPolicy.IsOutside(bounds, new() { X = 99, Y = 300 }) && MenuDismissPolicy.IsOutside(bounds, new() { X = 420, Y = 300 }) && MenuDismissPolicy.IsOutside(bounds, new() { X = 200, Y = 600 }));
            Check("左中右及附加键与外部滚轮均可关闭", new[] { 0x201, 0x204, 0x207, 0x20B, 0x20A, 0x20E }.All(MenuDismissPolicy.IsDismissPointerMessage));
            Check("单纯移出菜单不会关闭", !MenuDismissPolicy.IsDismissPointerMessage(0x200));
            Check("旧关闭消息不影响新菜单", !MenuDismissPolicy.IsCurrent(8, true, 7) && MenuDismissPolicy.IsCurrent(8, true, 8));
            Check("隐藏菜单忽略延迟关闭消息", !MenuDismissPolicy.IsCurrent(8, false, 8));
            Check("菜单显示前的前台事件不关闭新菜单", !MenuDismissPolicy.IsNewEvent(100, 99) && !MenuDismissPolicy.IsNewEvent(100, 100) && MenuDismissPolicy.IsNewEvent(100, 101));
            Check("事件时间比较支持系统计时回绕", MenuDismissPolicy.IsNewEvent(uint.MaxValue - 2, 3) && !MenuDismissPolicy.IsNewEvent(3, uint.MaxValue - 2));
            Check("焦点重试允许原来源窗口", MenuFocusPolicy.CanRetry(10, 20, 10));
            Check("焦点重试允许当前菜单", MenuFocusPolicy.CanRetry(10, 20, 20));
            Check("焦点重试拒绝其他应用和无来源", !MenuFocusPolicy.CanRetry(10, 20, 30) && !MenuFocusPolicy.CanRetry(0, 20, 10));
            var desktopContext = backgroundContext with { SourceWindow = 1 };
            var refresh = ActionCatalog.All.Single(x => x.Id == "refresh");
            Check("刷新默认位于一级且只支持背景视图", refresh.Primary && refresh.Supports(desktopContext) && !refresh.Supports(single));
            Check("虚拟背景刷新不依赖物理路径", refresh.Supports(virtualContext with { SourceWindow = 1 }));
            List<MenuAction> refreshBuiltin = [new() { Id = "refresh", Title = "刷新" }];
            Check("合并原生刷新避免重复入口", MenuComposer.DuplicatesBuiltin(new() { Title = "刷新", Verb = "refresh" }, refreshBuiltin)
                && MenuComposer.DuplicatesBuiltin(new() { Title = "刷新(E)" }, refreshBuiltin)
                && MenuComposer.DuplicatesBuiltin(new() { Title = "Refresh" }, refreshBuiltin)
                && !MenuComposer.DuplicatesBuiltin(new() { Title = "刷新(E)" }, [])
                && !MenuComposer.DuplicatesBuiltin(new() { Title = "刷新", Group = "第三方" }, refreshBuiltin));
        }
        catch (Exception ex) { results.Add(new("逻辑检查异常", false, ex.ToString())); }
        return results;
    }
}

