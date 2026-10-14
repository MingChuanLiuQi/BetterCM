# 1.0.1 更多项目闪烁修复及两份只读审查的复核

2026-10-13。版本号按用户要求保留 1.0.1。两份外部审查仅作为线索；以下区分源码事实、实际修复和没有足够证据的推断。

## 本轮落实

- **展开闪烁**：开发逐帧检查实测到独立 WPF 行动画在尺寸变化后透明度从 0.554 回退到 0.506。现在增高、透明度和位移共用一个 Stopwatch 进度。内容层只调整到最终尺寸，先完成准备帧再开始展开；终点不再次 Measure/Arrange 或清除视觉状态。取消、搜索、关闭和重开仍清理旧回调。复用行换 DataContext 时恢复视觉状态。
- **工作区底部的内容错位**：实机展开发现父窗口向上移动，而 layered child 留在旧屏幕位置，顶部出现空白。逐帧显式同步内容窗口位置，保持内容位图尺寸不变。终点发布最新观察状态。
- **延迟分组点击焦点**：每个令牌记录独立的请求、代次和焦点意图。正在悬停加载时再点击会升级该请求，旧分组返回不能抢开当前分组，同一令牌不重复查询。
- **隐藏搜索入口**：根菜单紧凑模式已有 CompactMenu 守卫；另明确禁止侧边窗口调用 FocusSearch。普通根菜单 Ctrl+F 仍会先展开更多，然后聚焦可见搜索框。
- **数字及别名快捷键**：0–9 映射为 D0–D9，支持 INS/PGUP/PGDN，拒绝把多位数字当 WPF Key 的底层整数解析。
- **设置覆盖外部更新**：保留未保存编辑并禁用保存，加载最新已保存配置后恢复。Save_Click 再检查配置快照，菜单固定操作成功后立即通知设置窗口。
- **剪贴板格式**：移除独立 Preferred DropEffect 的传输判定；保留 CF_HDROP、Shell IDList Array、Unicode 和 ANSI 虚拟文件描述符，不能按审查建议删去 CF_HDROP/ANSI 描述符。
- **文件定位**：增强模式“在资源管理器中定位”的文件路径使用 SHParseDisplayName + SHOpenFolderAndSelectItems，避免 Explorer 特殊命令行的引号问题。目录打开行为保留。实机空格、中文和 & 文件已经正确选中。

## DeepSeek 各项取舍

| 条目 | 复核与处理 |
| --- | --- |
| 1 隐藏搜索可聚焦 | 报告对紧凑模式的时序忽略了已有 `!CompactMenu` 守卫；而 WPF 键盘焦点要求 IsVisible=true。参见 [Microsoft Focus Overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/focus-overview)。侧边 Ctrl+F 确实没有产品语义，已统一搜索入口能力判断。不能把隐藏控件必然获得键盘焦点当成已实测事实。 |
| 2 延迟分组共享焦点 | 已修复真正的“加载中再次点击没有升级 focus”问题。报告称旧 A 会抢开，实际上已有 request/代次守卫阻止该步骤；新增回归同时检查过期 A 不打开及 B 获取焦点。 |
| 3 一会话多次新建 | ShellBridge.InvokeAsync 预留当前连接并切换到备用连接，当前菜单关闭；下一次右键创建新会话，不存在所述“同一 site 连续执行两次新建”的正常使用路径。不采用 pendingEdits 替换 TCS：它不能等待尚未到达的回调，反而会取消现有新建宽限期。 |
| 4/5.1 宿主硬上限 | 不采用 120 秒强退；用户仍在查看属性对话框或原生任务仍在运行时，这会截断操作。仅统计“我们创建的窗口”也会漏掉真正需要保留的第三方对话框。长期常驻扩展的资源诊断可继续研究，本轮不声称所有扩展都可回收。 |
| 5.2 搜索 | 与第 1 项一起复核并加能力守卫。 |
| 6.1 导航栏空白回退选区 | 报告描述的选区替代与当前 App catch 不符：catch 使用新的空 SelectedContext，经典失败菜单不会执行旧选区动作。不把模糊空白节点猜成“此电脑”。 |
| 6.2 设置外部变化 | 已加入保存冲突保护及加载最新配置后的恢复检查。 |
| 6.3 文本剪贴板 | Preferred DropEffect 是元数据而不是载荷，已收紧；ANSI 文件描述符仍是有效文件传输格式，应保留。真实复制/剪切粘贴继续回归。 |
| 6.4 环保护 | ReadMenu 的 visited 在入口 Add(menu) 并拒绝重复；延迟路径还通过 _submenuHandles 复用 token，CompleteMenu 有 token ancestors 检查。空集合是一次遍历的起始集合，并非关闭保护。不添加永久 visited 以免误删后续合法读取。 |
| 6.5 错误原因展示 | 真实原因已记日志，Shell 查询错误区域可展示后端错误。上下文失败仍保留简短降级提示；未把底层长异常直接铺在菜单上。 |
| 6.6 未使用声明 | 不作无关的大范围删除。本轮新增 ClientToScreen 用于明确验证及同步父子窗口位置。 |

## Gemini 各项取舍

| 条目 | 复核与处理 |
| --- | --- |
| 1 多标签页 HWND 匹配 | 不能仅凭“所有标签都注册相同 HWND”推断本机 Shell Automation 必然返回后台标签。绑定已有 ValidateLocation/选区核验，会拒绝位置变化。本轮没有多标签实机复現证据，不贸然更换 COM 绑定，也不声称已根治所有标签场景。 |
| 2 reveal 的引号 | 已改原生选择接口，并以实际文件选中结果验证；API 的 cidl=0 表示传完整项目 PIDL，打开父目录并选中该项目，参见 [Microsoft SHOpenFolderAndSelectItems](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shopenfolderandselectitems)。 |
| 3 数字快捷键 | 已修复并加入独立数据用例。 |
| 4 Height 不同步 | WPF 实际处理 WM_SIZE 后会更新公开 Height/ActualHeight，既有检查也验证过。为避免时序依赖，Reflow 起点直接读取当前 HWND 像素高度；不额外写 Height 触发重复窗口调整。 |
| 5 固定被设置覆盖 | 已加入配置快照冲突判断及同步通知。 |
| 6 选区绑定失败降级 | 不吞掉位置/选区校验异常。盲目切换独立 IShellFolder 可能执行过期目标，或丢失原视图的重命名服务，应保持拒绝并重新呼出。 |
| 7 首次自定义目录 | ConfigStore.Load 在无文件时会 Save，Save 已建目录。权限不足导致 Save 失败应报告，简单重复 CreateDirectory 不能修复权限。没有复现所述普通首次启动缺目录崩溃。 |
| 8 UIA 节点销毁 | 目标在捕获时销毁应取消/降级，不能改成沿用旧选区。当前异常在 host/App 边界被处理，不是进程无保护崩溃。 |
| 9 离线图标队列 | 风险合理，但报告没有离线实测。网络图标隔离与通用图标降级需要独立性能/准确性设计，本轮未扩大到图标执行架构，也不将超时等同于已取消底层调用。 |
| 10 日志轮转 | 合理的长期维护事项；现有日志有跨进程互斥保护。本轮不扩大到日志保留策略，仍列为后续改进。 |

内容窗口的位置同步还显式向 HwndTarget 通知尺寸未改变的 WM_SIZE：单纯检查当前子窗口坐标不够，Windows 自动移动后裔时 WPF 仍可能保留旧屏幕坐标，下次分层提交将其恢复。实测部分帧会偏离 3–213 像素。分层 HwndTarget 的 OnResize 会更新屏幕坐标并完成渲染同步；传入的是内容窗口真实且未改变的尺寸，不是每帧重建位图大小。同时阻止同步时的嵌套 Rendering 再次调整几何。依据 [dotnet/wpf HwndTarget 的窗口位置及尺寸处理](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/InterOp/HwndTarget.cs)。没有使用反射或访问框架私有字段。

混合 DPI、远程桌面、所有外部扩展和长期离线网络没有在本轮全面覆盖。最终构建身份、报告和实际验证范围见 QA-1.0.1-more-fix.md。
