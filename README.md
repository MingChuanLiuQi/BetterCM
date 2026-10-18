# BetterCM 1.8.1

BetterCM 为 Windows 11 的经典右键菜单提供现代外观。1.8.1 修复维护目录漏匹配、未签名重建残留证据和缺失程序的菜单状态，并明确区分最终安装包校验与 QA 标识安装测试。当前验证见 [QA-1.8.1](docs/releases/1.8.1/QA-1.8.1.md)，逐条处理见 [审查实施记录](docs/current/REVIEW-IMPLEMENTATION.md)。

## 安装与使用

本地主仓库打包输出为 `artifacts/releases/1.8.1/BetterCM-1.8.1-Setup-x64.exe`，同版本内部副本位于 `artifacts/internal/1.8.1/`。当前包未签名，尚未上传公开发行；公开发行后请使用对应 Releases 附件。历史安装包保存在 `artifacts/releases/<version>/`。安装包包含 .NET/WPF 和 Windows App SDK 材质组件，无需另装运行时；可安装到原目录升级，沿用用户配置。默认目录是当前用户的 `%LOCALAPPDATA%\Programs\BetterCM`，不需要管理员权限。安装时可选择登录自启或桌面快捷方式，默认均不勾选。

安装包尚未使用发行者代码签名。可用 `Get-FileHash .\artifacts\releases\1.8.1\BetterCM-1.8.1-Setup-x64.exe -Algorithm SHA256`，与同目录 `.exe.sha256` 文件核对。构建来源与完整载荷清单记录在版本验证文件中。

- 普通右键接管资源管理器文件视图、导航栏和桌面；开始按下前按住 Shift，可以使用原生菜单或原生右键拖动。
- 默认“使用经典模式（推荐）”保持 Shell 返回的顺序、分隔线、子菜单、禁用、勾选、默认动作及访问键。只改变呈现，不重新合成 Git、打开方式、路径等分组，不自动插入内置或自定义命令。
- 经典模式直接显示全部一级项目，“显示更多项目”、隐藏和固定到一级菜单等增强偏好不参与重排。原生子菜单悬停或点击后向侧边打开，空间不足时向左展开。搜索直接显示带完整祖先路径的可执行叶子，例如“NanaZip · SHA-256”；小尺寸模式关闭搜索并使用紧凑行高。
- 选择动作后整个菜单关闭，继续操作时请再次右键；再次新建会获得新的原生菜单与名称编辑过程。
- QQ 音乐等程序自行注册的文件夹命令会如实显示。本机原版经典菜单也有“使用 QQ音乐 播放”和“加入 QQ音乐 播放队列”；它们不是 BetterCM 添加的动作。
- 剪切、复制、粘贴、删除和属性调用当前 Shell 会话中的命令，保留原生状态、确认和对话框。重命名及新建后的名称编辑绑定原来的资源管理器视图，不使用命令行或模拟 F2。
- 多标签页按当前文件视图读取目录和选区；切换到另一个标签，即使目录相同，也拒绝执行旧菜单。
- 固定导航项目使用当前 Home 项目及 `System.Home.IsPinned` 状态，不依赖 UIA 是否包含“已固定”字样，也不缓存固定顺序。
- 普通根菜单 Ctrl+F 展开并定位搜索；紧凑模式及侧边子菜单不进入隐藏搜索框。向下键进入可执行结果；Enter 执行选中项或系统默认项（无默认项时不执行），左右键进入或返回子菜单，Esc 关闭。经典访问键使用 Shell 原来的字母；菜单行及材质父窗口禁用 IME 组合输入，独立搜索内容窗口仍启用 IME；F5 刷新绑定的原生背景视图。Ctrl+C/X/V、F2、Delete、Alt+Enter 绑定原生能力，打印数字快捷键按实际数字键匹配。
- 外部输入、前台切换、窗口移动或最小化关闭整个菜单链，并将外部操作继续传给原窗口。Shell 读取期间的新输入会取消旧准备，不补发延迟的原生菜单。
- 设置可选择系统/深色/浅色、Mica/Acrylic/纯色，以及主菜单和侧边子菜单的动效。动画终点不重新移除视觉时钟或重建材质，只播放一套内容动画。
- 默认呼出热键为 Alt+X，备用为 Ctrl+Shift+X；设置显示实际注册结果，格式错误时即时提示。撤销未保存的修改会取消未保存编辑和预览。预览准备期间被外部输入取消也立即恢复已保存外观；搜索先过滤已有项目，再补全慢速子菜单。
- 关闭设置后继续驻留托盘，左键打开设置，托盘菜单可预览、暂停接管或退出。登录自启使用 `--background`；再次运行打开已有设置，不创建第二个常驻实例。
- 配置读取失败时暂停保存并保留原文件；设置页提供重新读取和恢复上次备份入口。正常保存使用同目录原子替换，上一份配置保留为 `config.json.bak`。未知配置格式会被拒绝，避免旧程序覆盖新格式。
- 系统动作可等待超过 30 秒的操作和确认框，不因统一等待上限误报失败；仍可打开下一份菜单。升级时先请求主程序正常退出；若系统任务仍占用文件，请等任务完成后重试安装。

支持 Windows 11 x64。ARM64 尚未发布验证。

BetterCM 不提供开始菜单、任务栏的固定及取消固定入口；确需操作时，按住 Shift 再右键，使用 Windows 自己的菜单。这不影响“固定到快捷访问”或增强模式的“固定到一级菜单”。

## 保留的增强设置

以前的内置动作、自定义动作、隐藏和固定偏好均保留在配置中。需要使用时，手动取消“使用经典模式（推荐）”。增强模式沿用以前的路径格式、全文复制、SHA256、程序打开、一级菜单管理及更多折叠；这些设置默认不影响经典模式。

自定义参数支持 `{path}`、`{dir}`、`{name}` 和整个选区的独立 `{paths}` 参数。先按 Windows 参数规则分词，再替换并通过 ArgumentList 传给程序；支持单选、多选逐个和一次批量，扩展名使用 `.txt`、`.json`、`*` 或 `dir`。

设置窗口构造完成即加载配置和经典模式禁用态；外部配置更新时保留编辑并提示用“撤销未保存的修改”加载最新值。保存配置后才写启动项；写入失败分别还原原始配置文本和启动项值，回滚失败明确提示。主热键注册失败显示托盘警告。

用户配置和日志位于 `%LOCALAPPDATA%\BetterCM`。日志按 2 MiB 轮转，保留 BetterCM.log 与 BetterCM.prev.log，超长异常条目限长。安装目录的 config.json 只用于首次初始化；无效配置保留原文件和当前有效配置。升级、卸载保留用户配置。旧配置中没有 ClassicMenu 字段时，升级默认使用经典模式。

## 构建与验证

开发 SDK 由 global.json 固定为 .NET 10.0.401（允许同一补丁带更新）；安装包需要 Inno Setup 6.5 或以上。

~~~powershell
dotnet test .\Tests\BetterCM.Tests.csproj -c Release
.\verify.ps1
.\Build-Installer.ps1
~~~

verify.ps1 执行脚本回归、锁文件恢复、传递依赖漏洞审计、警告视为错误的构建、xUnit、自带运行时发布和 UI/Shell 自检。Build-Installer.ps1 要求主仓库干净提交，经过同一门禁后生成安装包、SHA256 和带 SourceCommit 的 payload 清单。发布目录为 artifacts/publish/win-x64，报告在 artifacts/qa。

GitHub Actions 配置涵盖锁文件恢复、漏洞审计、构建/测试/发布和安装器编译；桌面检查通过可选交互 runner 作业执行，另有 Dependabot 和 CodeQL 配置。无桌面作业显式使用 -SkipDesktopChecks，其安装包仅作为内部制品。远端工作流与交互 runner 尚未实跑，不据本机通过结果推定其状态。依赖审计报告中的项目路径已改为仓库相对路径。

版本唯一来源是 BetterCM.csproj 的 Version，AppInfo 从程序集读取；manifest、可执行文件、设置、托盘、报告和安装包派生同一版本。artifacts、Backups、Tests 排除在产品默认资源扫描之外。

~~~powershell
.\artifacts\publish\win-x64\BetterCM.exe --self-test .\artifacts\qa\manual-self-test.json
.\artifacts\publish\win-x64\BetterCM.exe --stress-test .\artifacts\qa\stress-1.8.1.json
.\artifacts\publish\win-x64\BetterCM.exe --input-check .\artifacts\qa\input-1.8.1.json
.\artifacts\publish\win-x64\BetterCM.exe --cascade-input-check .\artifacts\qa\cascade-1.8.1.json
~~~

以上是生成报告的命令示例，不表示这些输出文件已经存在。本次实际执行的检查、输出位置和未覆盖范围以 [QA-1.8.1](docs/releases/1.8.1/QA-1.8.1.md) 为准。

自检使用新建的临时夹具进行真实原生复制、移动及无副作用的视图刷新，检查回收站命令但不清空用户回收站。压力检查使用 40 秒假命令及卡住捕获夹具。两种输入检查需要按界面提示操作，只调用记录委托。

已安装版本存在时，使用独立标识测试包验证安装和卸载，不替换真实安装：

~~~powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DTestBuild .\Installer\BetterCM.iss
.\Installer\Test-Installer.ps1 -Isolated
~~~

## 当前边界

最终 Setup 仅完成哈希、载荷与源码一致性校验；实际安装/卸载测试使用共享载荷的 QA TestBuild 标识。没有可丢弃 Windows 11 环境，用户已选择暂缓最终正式标识安装验证。当前为可追溯的本地未签名测试构建，不能称为“已验证的正式发布”。

经典导入基于 IContextMenu/IContextMenu2/3；无文字的自绘项目和仅提供 Windows 11 新接口的扩展可能无法完整导入，Shift+右键保留直接系统入口。Explorer 自己加入的导航树展开等视图控制项与 Shell 扩展菜单并非同一接口。跨目录多选不导入 Shell 菜单；不同设备、混合 DPI、多标签、远程桌面及扩展组合仍需继续验证。

读取系统菜单后按最终尺寸首次显示，因此慢扩展可能延后出现；执行超时只断开通信，保留执行宿主及原生操作。视图位置或选区变化时拒绝执行旧快照。安装包尚未使用发行者代码签名。

历史完整备份与发布包继续保留；本轮维护只对较旧构建目录先压缩、逐文件校验再移除。旧备份线索见 [历史备份证据](docs/history/backup-evidence.md)。

详见 [更新记录](CHANGELOG.md)、[架构](ARCHITECTURE.md)、[当前验证记录](docs/releases/1.8.1/QA-1.8.1.md) 和 [本轮审查处理记录](docs/current/REVIEW-IMPLEMENTATION.md)。项目源码按 [MIT 许可证](LICENSE) 授权。图标的独立来源与授权尚待确认，第三方许可见 [THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES.md)。


## 审查加固与开发

界面目前仅提供简体中文。源码按 MIT 授权，第三方组件按各自许可分发。

- 统一入口为 `BetterCM.slnx`；共享设置与依赖版本分别位于 `Directory.Build.props`、`Directory.Packages.props`。
- 运行 `./verify.ps1` 检查锁文件、依赖漏洞、Release 编译、测试、自包含发布及桌面自检；无交互桌面时显式使用 `-SkipDesktopChecks`，UI 变更仍需提供手工 QA 证据。
- 自定义程序在编辑时固定为绝对 `.exe` / `.com` 路径；脚本需显式选择解释器。程序文件暂时缺失时，菜单禁用该动作并显示原因，保留配置以支持可移动介质；重新打开菜单会再次检查可用性。旧 schema 1 配置在内存中迁移至 2，不可解析的旧动作会禁用并保留供编辑修复。保存前不会改写原文件，保存后保留旧配置备份；旧程序不支持 schema 2。
- 设置的诊断页可“导出诊断包”。包含版本、系统、DPI、脱敏配置和最近日志摘要；路径、用户名、文件名、自定义标题及参数、原始日志消息与堆栈均不导出。分享前仍请查看包内容。
- `./Maintenance.ps1` 默认仅显示计划；`-Apply` 创建源码快照和完整 Git bundle，校验压缩包后才移除较旧的 dev（含 dev2–dev9）、installer、QA compiler、QA installer 和 script-fixture 目录。按这些类别分别保留最近 3 个目录、所有已有历史备份、release payload 和 QA 原目录。常见未跟踪源码和文档自动纳入，`-AdditionalSource` 可显式加入其他必要文件，运行时应停止其他构建。`-SkipQaArchive` 可跳过重复压缩 QA 原目录，现有 QA 证据仍保留。成功的脚本回归夹具在 finally 中移除，失败夹具保留供排查并受维护保留规则约束。该脚本使用类型白名单识别未跟踪源码，不将构建产物混入源码备份。
- 安装包与校验文件不再跟踪进 Git。完整发布输出在 `artifacts/releases/<version>/`；跳过桌面验证的内部包在 `artifacts/internal/<version>/`，上传公开下载应使用 Releases 或制品库。
- 没有证书也能构建未签名包。以后取得代码签名证书，可用 `./Build-Installer.ps1 -RequireSigning -CertificateThumbprint <指纹> -SignToolPath <Windows SDK 中 signtool.exe 的完整路径>`，为自有程序、安装包及卸载器签名并加时间戳；当前未声称任何历史安装包已签名。

本次变更和外部待办见 [审查实施记录](docs/current/REVIEW-IMPLEMENTATION.md)，历史验证记录在 [docs/releases](docs/releases/)。


## 本轮复查与发布边界

自定义动作默认使用程序所在目录作为工作目录。只有在编辑窗口勾选“使用目标文件夹作为工作目录”后才切换到目标目录；此选项可能让第三方程序从目标目录加载 DLL/插件，请仅对确有需要的程序启用。`{dir}` 参数的含义不受该选项影响。旧配置缺少该选项时默认关闭。

首次初始化会在程序名解析前清理旧的 bundled 默认动作；用户配置中的自定义动作不会因此被删除。恢复 schema 1 备份会将迁移后的当前格式写回配置文件，同时保留备份原文和损坏副本。

正式签名发布目前明确阻塞于代码签名证书：没有证书，未完成真实签名、时间戳及信任链的成功演练。`SignedUninstaller` 的临时卸载器由签名回调按架构保存为 `signed-uninstaller-e32.exe` / `signed-uninstaller-e64.exe` 并校验；未签名重建会清除旧证据，不依赖编译结束后查找安装目录的 `unins000.exe`。脚本回归使用测试替身验证回调文件处理，不能代替真实签名演练。

`Test-Installer.ps1` 默认是 Release 门禁，必须存在主仓库对应的最终安装包和匹配的 payload 清单；只验证内部包时必须显式传入 `-ReleaseKind Internal`，报告会标明类型。候选源码 bundle 继续作为历史过程证据，本轮发布源改为主仓库提交和本地 annotated tag `v1.8.1`。没有执行远端推送。

`Maintenance.ps1 -Apply -KeepRecent 3` 保留已有历史备份、所有 release payload 和 QA 原目录，回收量来自压缩后的旧构建目录，不能声称会删除或回收全部 53 GB 备份。额外加入 `-IncludeVerificationFixtures` 可将本轮已保留源码 bundle 的 clean-build-fixture 目录先归档校验再移除。默认不执行该额外范围。
