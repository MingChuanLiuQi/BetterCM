# BetterCM 0.7.0 项目交接

工作区 C:\Users\zky89\Desktop\文件夹\BetterCM。基线 HANDOFF-0.6.0.md。本轮于 2026-10-12 开发、打包并进行主要验证，额度中断后于 2026-10-13 完成最终输入回归与交付。用户要求先备份，集中整顿后端，并加入真正全项目和无搜索小尺寸模式。

## 当前交付

正式安装包位于根目录 BetterCM-0.7.0-Setup-x64.exe，61,132,219 字节，0.7.0.0，SHA256 7B6EECE8CCC4EF33B233A0DA6BEE2A31A88D1E4BD63113414B5CFB3C073FEA1C。同目录有 sha256。自带 .NET/WPF 和 Windows App SDK 材质组件。

修改前备份 Backups/BetterCM-0.6.0-before-0.7.0-20261012-162544.zip，2917 文件全部验证，1,474,318,155 字节。SHA256 539D54E35E209739F134D1687B6D951B05EE5BC8D3449DFEE02834F71EEE073D。旧包、历史备份保留。

真实安装仍为 0.6.0.0，后台已恢复，配置与启动项未改变。测试实例及隔离安装已退出/卸载。不要在后续声称已自动安装 0.7.0；用户可运行根目录安装包升级。

## 后端调整与根因

1. 新增 ShellCommandMap：open/cut/copy/paste/rename/delete/properties 只绑定根层 canonical verb。UI 状态、执行委托、ShellKey 来自本次原生菜单，标准入口等待 Shell 时禁用，不再因内置动作定义支持路径而假装可用。IncludeShellActions 控制额外原生项目显示，标准动作仍读取原生能力。
2. 删除 ActionExecutor 自建 FileDrop/Preferred DropEffect 的剪切复制实现及旧格式检查。标准动作走原生 IContextMenu.InvokeCommand；原生剪贴板对象通过 Clipboard.Flush 跨宿主退出保留。添加真实 ShellOperationChecks，实际复制/剪切并核对文件结果。
3. 原独立宿主没有显式 OleInitialize，原生 copy 在初轮实测返回 E_FAIL。ShellHostLifetime 现在显式初始化 OLE，并设置 Shell 线程/进程生命周期引用。InvokeCommand 加 CMIC_MASK_NOASYNC；客户端授予宿主前台权限，宿主激活来源窗口。IPC 结束后保持 STA，直到依赖引用和可见原生窗口消失。继续遵守调用超时/Dispose 不 Kill 执行宿主原则。
4. 实机 Explorer GetItemObject(SVGIO_SELECTION) 的跨进程包装器列出命令但不返回 canonical verb，不能用它做标准按钮映射。对象菜单改用本地 SHBindToParent/GetUIObjectOf；保留 ShellViewBinding 作为原视图/完整选区校验。只在单选且有视图时声明 CMF_CANRENAME，rename 用 IShellView.SelectItem(SVSI_EDIT | SELECT | DESELECTOTHERS | ENSUREVISIBLE | FOCUSED) 进入原生内联编辑，没有命令行、模拟按键或 PowerRename 依赖。
5. 背景仍用真实 IShellView 获取查看/排序。无视图的 CreateViewObject 背景菜单不提供标准 paste，部分跨进程包装器不提供其规范名：缺少 paste verb 时读取同目标文件夹的原生对象菜单，补入 paste/pastelink，使用 FolderCommandBase 会话内偏移路由回原 IContextMenu。两份同目标原生菜单相同粘贴标题仅用于展示去重，执行不按标题猜测。绑定视图执行前仍校验原位置。
6. ShellItem 稳定键重复时加入出现顺序后缀；UI Id 包含 Command 与 Key。某些扩展会复用 command ID 给禁用占位项和有效动作，不可只按 Command 去重。GetCommandString 检查 HRESULT，Command offset 依据 SHELL_COMMAND_FIRST。
7. 执行操作时立即关闭 BetterCM 菜单，避免关闭动画干扰原生编辑/对话框；一般取消仍保留原动效。

回收站沿用原 namespace IContextMenu 的 empty 命令，未替换为脚本。OLE/调用/宿主生命周期是本轮对这条公共执行路径的修复。自检确认真实 empty 命令及 Enabled 状态，未实际清空用户回收站，也未验证清空确认对话框；不要把读取/映射检查宣称为完整清空实测。

## 全项目与小尺寸

ShowMoreItems=false 现在调用 MenuComposer allItems 分支直接列出所有嵌套叶子（来源路径出现在标题），不再仅自动展开“更多”后留下折叠组。保留普通模式搜索。

新增 AppConfig.CompactMenu，默认 false，旧配置自然补默认值。小尺寸强制全项目、300 DIP 宽、12 DIP 字号、较小 Padding，禁用 Ctrl+F 搜索，隐藏搜索/更多/图标工具栏；工具栏动作进入文字列表。长菜单受工作区和 640 DIP 高度限制，通过滚动查看全部项目。设置页独立开关、保存、预览和还原覆盖此字段。

保持 0.6.0 布局/材质机制：透明 WPF 子窗口一次设置最终尺寸，非分层父窗口负责尺寸/展开裁切；不逐帧设置 Window.Height/子位图尺寸。保留 Mica 独立活动态、输入完整手势、Shell 隔离宿主。

## 最终验证

零警告/错误构建发布，101/101 xUnit，最终发布91/91，隔离安装91/91，压力58/58，安装卸载13/13，最终发布真实输入2/2。报告均在 artifacts/qa，详细记录 QA-0.7.0.md / QA.md。

实际 UI：小尺寸列表、Shell 标准入口、背景粘贴已观察；BetterCM F2 进入 Explorer 内联编辑后取消；Alt+Enter 属性窗口持续存在并正常关闭；BetterCM Ctrl+X → 打开目标目录 → 背景 BetterCM Ctrl+V，源消失、目标存在。临时文件在 artifacts/qa/live-070/目标文件夹。live-transfer-0.7.0.json 保存观察快照。最后前台授权微调之后重跑发布/安装自检、压力及真实输入；未重新声称一整套实体文件操作矩阵。

压力测试第二次 open 34.13 ms，50 次 IPC p50 0.25 ms/max0.54 ms，捕获超时1802.22 ms、后续210.20 ms。这是受控测试，不是任意第三方扩展或实体连击速度。

本轮早期测试曾把 OleGetClipboard 的延迟代理传回 OleSetClipboard，造成递归数据提供与测试进程错误；已改为先读取数据格式进行快照再恢复，最终检查全部通过。测试中不要恢复该做法。

仍存在 classic IContextMenu/带文字叶子的支持边界；导航栏或剪贴板等无法确认原视图的目标不冒用其他选区做内联重命名；保留 Shift+右键。没有 Git 仓库/远端，未运行远端 CI。

主要新增文件：Services/ShellCommandMap.cs、ShellHostLifetime.cs、ShellOperationChecks.cs、Tests/ShellMappingTests.cs。其他变化在 ShellMenuSession/ShellBridge/ShellViewBinding、App、ActionExecutor、ActionCatalog、SelectedContext/ExplorerContextService、MenuComposer、ContextMenuWindow、MainWindow、Config 与回归检查。
