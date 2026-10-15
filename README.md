# BetterCM 1.5.1

BetterCM 为 Windows 11 的经典右键菜单提供现代外观。1.5.1 移除无法可靠执行的“固定到开始／任务栏”及对应取消固定入口，撤回 1.5.0 的二次系统菜单确认方案。快捷访问固定与 BetterCM 一级菜单管理保持原有行为。保留 1.5.0 的各级 Thin Acrylic、短菜单测量、同目标在飞读取和配置快照修复。验证见 QA-1.5.1.md，取舍见 REVIEW-1.5.1.md。

## 安装与使用

运行项目根目录的 **BetterCM-1.5.1-Setup-x64.exe**。安装包包含 .NET/WPF 和 Windows App SDK 材质组件，无需另装运行时；可安装到原目录升级，沿用用户配置。默认目录是当前用户的 `%LOCALAPPDATA%\Programs\BetterCM`，不需要管理员权限。安装时可选择登录自启或桌面快捷方式，默认均不勾选。

- 普通右键接管资源管理器文件视图、导航栏和桌面；开始按下前按住 Shift，可以使用原生菜单或原生右键拖动。
- 默认“忠实呈现经典右键菜单”保持 Shell 返回的顺序、分隔线、子菜单、禁用、勾选、默认动作及访问键。只改变呈现，不重新合成 Git、打开方式、路径等分组，不自动插入内置或自定义命令。
- 经典模式直接显示全部一级项目，“显示更多项目”、隐藏和固定到一级菜单等增强偏好不参与重排。原生子菜单悬停或点击后向侧边打开，空间不足时向左展开。搜索直接显示带完整祖先路径的可执行叶子，例如“NanaZip · SHA-256”；小尺寸模式关闭搜索并使用紧凑行高。
- 选择动作后整个菜单关闭，继续操作时请再次右键；再次新建会获得新的原生菜单与名称编辑过程。
- QQ 音乐等程序自行注册的文件夹命令会如实显示。本机原版经典菜单也有“使用 QQ音乐 播放”和“加入 QQ音乐 播放队列”；它们不是 BetterCM 添加的动作。
- 剪切、复制、粘贴、删除和属性调用当前 Shell 会话中的命令，保留原生状态、确认和对话框。重命名及新建后的名称编辑绑定原来的资源管理器视图，不使用命令行或模拟 F2。
- 多标签页按当前文件视图读取目录和选区；切换到另一个标签，即使目录相同，也拒绝执行旧菜单。
- 固定导航项目使用当前 Home 项目及 `System.Home.IsPinned` 状态，不依赖 UIA 是否包含“已固定”字样，也不缓存固定顺序。
- 普通根菜单 Ctrl+F 展开并定位搜索；紧凑模式及侧边子菜单不进入隐藏搜索框。向下键进入可执行结果；Enter 执行选中项或系统默认项（无默认项时不执行），左右键进入或返回子菜单，Esc 关闭。经典访问键使用 Shell 原来的字母；菜单行及材质父窗口禁用 IME 组合输入，独立搜索内容窗口仍启用 IME；F5 刷新绑定的原生背景视图。Ctrl+C/X/V、F2、Delete、Alt+Enter 绑定原生能力，打印数字快捷键按实际数字键匹配。
- 外部输入、前台切换、窗口移动或最小化关闭整个菜单链，并将外部操作继续传给原窗口。Shell 读取期间的新输入会取消旧准备，不补发延迟的原生菜单。
- 设置可选择系统/深色/浅色、Mica/Acrylic/纯色，以及主菜单和侧边子菜单的动效。动画终点不重新移除视觉时钟或重建材质，只播放一套内容动画。
- 默认呼出热键为 Alt+X，备用为 Ctrl+Shift+X；设置显示实际注册结果，格式错误时即时提示。还原为已保存会取消未保存编辑和预览。预览准备期间被外部输入取消也立即恢复已保存外观；搜索先过滤已有项目，再补全慢速子菜单。
- 关闭设置后继续驻留托盘，左键打开设置，托盘菜单可预览、暂停接管或退出。登录自启使用 `--background`；再次运行打开已有设置，不创建第二个常驻实例。

支持 Windows 11 x64。ARM64 尚未发布验证。

BetterCM 不提供开始菜单、任务栏的固定及取消固定入口；确需操作时，按住 Shift 再右键，使用 Windows 自己的菜单。这不影响“固定到快捷访问”或增强模式的“固定到一级菜单”。

## 保留的增强设置

以前的内置动作、自定义动作、隐藏和固定偏好均保留在配置中。需要使用时，手动取消“忠实呈现经典右键菜单”。增强模式沿用以前的路径格式、全文复制、SHA256、程序打开、一级菜单管理及更多折叠；这些设置默认不影响经典模式。

自定义参数支持 `{path}`、`{dir}`、`{name}` 和整个选区的独立 `{paths}` 参数。先按 Windows 参数规则分词，再替换并通过 ArgumentList 传给程序；支持单选、多选逐个和一次批量，扩展名使用 `.txt`、`.json`、`*` 或 `dir`。

设置窗口构造完成即加载配置和经典模式禁用态；外部配置更新时保留编辑并提示用“还原为已保存”加载最新值。保存配置后才写启动项；写入失败分别还原原始配置文本和启动项值，回滚失败明确提示。主热键注册失败显示托盘警告。

用户配置和日志位于 `%LOCALAPPDATA%\BetterCM`。日志按 2 MiB 轮转，保留 BetterCM.log 与 BetterCM.prev.log，超长异常条目限长。安装目录的 config.json 只用于首次初始化；无效配置保留原文件和当前有效配置。升级、卸载保留用户配置。旧配置中没有 ClassicMenu 字段时，升级默认使用经典模式。

## 构建与验证

开发 SDK 由 global.json 固定为 .NET 10.0.401（允许同一补丁带更新）；安装包需要 Inno Setup 6.5 或以上。

~~~powershell
dotnet test .\Tests\BetterCM.Tests.csproj -c Release
.\verify.ps1
.\Build-Installer.ps1
~~~

verify.ps1 执行警告视为错误的构建、无界面 xUnit、自带运行时发布和 UI/Shell 自检。Build-Installer.ps1 通过同一门禁后生成安装包及 SHA256。发布目录为 artifacts\publish\win-x64；最终报告和测试夹具位于 artifacts\qa。GitHub Actions 调用 verify.ps1 -SkipDesktopChecks，仅执行构建、xUnit 和发布；桌面 UI/Shell 检查由本地默认门禁执行，打包不会跳过。本地项目尚未上传，远端工作流未运行。

版本唯一来源是 BetterCM.csproj 的 Version，AppInfo 从程序集读取；manifest、可执行文件、设置、托盘、报告和安装包派生同一版本。artifacts、Backups、Tests 排除在产品默认资源扫描之外。

~~~powershell
.\artifacts\publish\win-x64\BetterCM.exe --self-test .\artifacts\qa\manual-self-test.json
.\artifacts\publish\win-x64\BetterCM.exe --stress-test .\artifacts\qa\stress-1.5.1.json
.\artifacts\publish\win-x64\BetterCM.exe --input-check .\artifacts\qa\input-1.5.1.json
.\artifacts\publish\win-x64\BetterCM.exe --cascade-input-check .\artifacts\qa\cascade-1.5.1.json
~~~

自检使用新建的临时夹具进行真实原生复制、移动及无副作用的视图刷新，检查回收站命令但不清空用户回收站。压力检查使用 40 秒假命令及卡住捕获夹具。两种输入检查需要按界面提示操作，只调用记录委托。

已安装版本存在时，使用独立标识测试包验证安装和卸载，不替换真实安装：

~~~powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DTestBuild .\Installer\BetterCM.iss
.\Installer\Test-Installer.ps1 -Isolated
~~~

## 当前边界

经典导入基于 IContextMenu/IContextMenu2/3；无文字的自绘项目和仅提供 Windows 11 新接口的扩展可能无法完整导入，Shift+右键保留直接系统入口。Explorer 自己加入的导航树展开等视图控制项与 Shell 扩展菜单并非同一接口。跨目录多选不导入 Shell 菜单；不同设备、混合 DPI、多标签、远程桌面及扩展组合仍需继续验证。

读取系统菜单后按最终尺寸首次显示，因此慢扩展可能延后出现；执行超时只断开通信，保留执行宿主及原生操作。视图位置或选区变化时拒绝执行旧快照。安装包尚未使用发行者代码签名。

修改前完整备份：Backups\BetterCM-1.2.0-before-1.3.0-20261014-032538.zip，17857 个文件逐一核对大小和 SHA256，附清单和压缩包校验文件。旧版本安装包及历史备份保留。

1.0.1 布局与性能迭代的历史备份为 Backups\BetterCM-1.0.1-before-layout-perf-20261013-192047.zip，12584 文件逐一校验；同目录保存旧安装包。该历史迭代按用户要求保留 1.0.1 版本号，用 SHA256 区分构建。

详见 [更新记录](CHANGELOG.md)、[架构](ARCHITECTURE.md)、[当前验证记录](QA-1.5.1.md)、[本轮审查处理记录](REVIEW-1.5.1.md) 和 [交接](HANDOFF-1.5.1.md)。
