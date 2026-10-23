# BetterCM

BetterCM 为 Windows 11 x64 的经典右键菜单提供现代外观。当前版本为 **1.9.1**，重点为网络程序探测隔离、真实菜单请求计时和发布身份核对。功能、验证范围及限制见 [1.9.1 迭代说明](docs/releases/1.9.1/ITERATION-1.9.1.md) 与 [当前 QA](QA.md)。[文档索引](docs/README.md) 区分当前说明与历史证据。

## 安装与使用

从 [GitHub Releases 下载 1.9.1 安装包或便携包](https://github.com/MingChuanLiuQi/BetterCM/releases/tag/v1.9.1)，同时提供校验文件、源码和发布证据。本版未签名；验证范围以 Release 说明和证据清单为准。

候选构建按次保存在 `artifacts/candidates/1.9.1-<UTC时间>-<标识>/`，其中 `package/` 放置 Setup、便携 ZIP、校验文件及发布证据，顶层附源码 ZIP、Git bundle 和 `candidate-manifest.json`。只有成功生成清单的目录才是完整候选；不能用目录名或版本号代替提交及哈希核对。默认安装到 `%LOCALAPPDATA%\Programs\BetterCM`，不需要管理员权限。安装包包含 .NET/WPF 与 Windows App SDK 材质组件，登录自启及桌面快捷方式默认均不勾选。

- 普通右键接管 Explorer 文件视图、导航栏和桌面；开始按下前按住 Shift，使用 Windows 菜单或原生右键拖动。
- 默认经典模式保持 Shell 的顺序、分隔线、子菜单、禁用、勾选、默认动作及访问键。子菜单向侧边展开，空间不足时向左打开；不额外插入增强动作。
- 普通菜单 Ctrl+F 展开搜索，搜索结果包含完整祖先路径。向下键选择，Enter 执行默认项或当前项，左右键进入/返回子菜单，Esc 关闭。Ctrl+C/X/V、F2、Delete、Alt+Enter 使用原生能力，F5 刷新绑定的原生背景视图。
- 重命名、新建后的名称编辑绑定原 Explorer 视图；多标签页切换或目标变化后拒绝执行旧菜单。选择动作后菜单关闭，再次右键取得新会话。
- 前台切换、窗口移动、最小化和外部输入关闭整个菜单链；Shell 准备期间的新输入取消旧请求。
- 设置支持系统/深色/浅色、Mica/Acrylic/纯色、紧凑菜单及各级动效。默认热键 Alt+X，备用 Ctrl+Shift+X。关闭设置后继续驻留托盘，左键图标打开设置。
- 手动关闭经典模式后，可用内置动作、自定义程序、隐藏和固定到一级菜单的偏好。自定义参数支持 `{path}`、`{dir}`、`{name}` 及整个选区的独立 `{paths}` 参数。替换只扫描原模板，文件名里的花括号保持原样。
- 自定义程序使用绝对 `.exe` / `.com` 路径；脚本需显式选择解释器。支持单选、逐个和一次批量。逐个模式在后台串行启动，提供进度和取消；取消停止后续启动，已启动的程序继续运行。程序缺失时禁用菜单项并保留配置。
- 自定义动作默认使用程序目录作为工作目录；仅显式勾选后使用目标目录。目标目录可能影响第三方程序加载 DLL/插件，请按需启用。
- 本地与网络程序探测分别最多四个并发；UNC 和映射网络盘使用独立线程，单次菜单等待仍不超过 250 ms。失败或超时的网络路径退避两秒，不占用本地探测名额。
- 诊断页显示最近一次真实输入请求的累计阶段耗时；导出诊断包包含最近 32 次已结束请求，区分取消、被后续请求替代、呈现及焦点状态，不包含目标路径或文件名。计时从接管右键松开或接收热键消息开始，呈现观测不代表显示器扫描或动画完成。
- 用户配置和日志位于 `%LOCALAPPDATA%\BetterCM`。保存采用原子替换，上一份配置保留为 `.bak`；配置损坏或格式过新时保护原文件并暂停保存。文件监听错误后会记录日志并重建监听。
- BetterCM 不提供开始菜单或任务栏固定入口；此类操作可用 Shift+右键。经典导入基于 IContextMenu/IContextMenu2/3，无文字自绘项、仅提供新菜单接口的扩展和跨目录多选存在边界。

## 构建、验证与发布

SDK 由 `global.json` 固定，安装器需要 Inno Setup 6.5+。版本唯一来源为 `Version.props`，程序集、manifest、设置、托盘、报告和安装包均派生自它。运行 `./Bump-Version.ps1 -Version 1.10.0` 可修改下一版本；脚本通过 MSBuild 求值读取版本。

```powershell
dotnet test .\Tests\BetterCM.Tests.csproj -c QA
.\verify.ps1
# 无交互桌面的构建机：
.\verify.ps1 -SkipDesktopChecks
# 只接受干净且已提交的主仓库：
.\Build-Installer.ps1
# 当前编辑内容的独立干净源码快照；不提交主工作区：
.\Build-Candidate.ps1
# 追加 QA 独立身份安装、启动、退出和卸载检查：
.\Build-Candidate.ps1 -InstallerSmoke
```

`verify.ps1` 执行 PowerShell 回归、锁文件恢复、传递依赖漏洞审计、whitespace 与 IDE0049 style 检查、Release/Debug/QA 解决方案构建、QA 与 Release 配置下的测试项目、两分钟挂起限制、Cobertura 覆盖率收集、自包含发布及正式二进制隔离检查。有交互桌面时另行发布 QA 程序并执行 UI/Shell 自检。报告在 `artifacts/qa`。

| 构建 | 输出 | 用途 |
| --- | --- | --- |
| Release | `artifacts/publish/win-x64` | 正式程序；无 QA 类型、入口及环境变量写报告逻辑 |
| QA | `artifacts/diagnostics/win-x64` | 自检、压力检查和输入检查；不进入正式安装载荷 |
| Debug | `bin/Debug/...` | 开发时启用 QA |

正常 Release 只接受无参数启动、`--background`、`--shutdown`、`--shell-host <pipe>` 和 `--context-host <pipe>`；其他参数以退出码 2 拒绝。直接构建测试项目时固定引用 QA 配置；`dotnet test Tests/BetterCM.Tests.csproj -c Release` 也会构建 QA 版项目引用。解决方案的 Release 配置只构建产品，不包含依赖 QA 的测试；Debug/QA 包含测试。运行解决方案测试请选择 QA，避免将 Release 的产品构建当作测试完成。

```powershell
.\artifacts\diagnostics\win-x64\BetterCM.exe --self-test .\artifacts\qa\manual-self-test.json
.\artifacts\diagnostics\win-x64\BetterCM.exe --stress-test .\artifacts\qa\manual-stress.json
.\artifacts\diagnostics\win-x64\BetterCM.exe --input-check .\artifacts\qa\manual-input.json
```

这些命令是操作示例；实际执行范围以本版 QA 记录为准。自检使用临时文件夹进行真实原生复制、移动及视图刷新，不清空用户回收站。输入检查仍需人工操作。

CI 将验证和安装器编译放在同一 Windows 作业，安装阶段使用 `-ReuseVerifiedPublish`。复用前重新核对干净提交、源码树 SHA256、版本、检查范围、完整文件集合及每个 SHA256；不能把跳过桌面检查的证据当作桌面通过。远端 CI 尚未在本轮执行。

`Build-Installer.ps1` 保留干净提交门禁。完整门禁产物进入 `artifacts/releases/<version>`；跳过桌面检查的安装包进入 `artifacts/internal/<version>`。两者均附便携包、`release-manifest.json` 和 `evidence/`。`Build-Candidate.ps1` 在独立快照仓库内调用同一门禁，再把结果放入候选目录；程序标识快照提交，清单同时记录原工作区提交、是否干净及逐文件相同的源码树哈希，不能冒充主仓库已提交或公开发布。

真实代码签名仍需要可用证书；当前没有证书，尚未完成签名、时间戳和信任链的成功演练。已有 `-RequireSigning -CertificateThumbprint <指纹> -SignToolPath <signtool路径>` 强制校验流程。本版替换了未知来源旧图标，新图标由 [生成脚本](Assets/Generate-Icon.ps1) 原创绘制，按仓库 MIT 授权。

## 工作区维护

`Maintenance.ps1` 默认仅预览。备份默认放在项目旁的 `BetterCM.Storage`，可用 `-BackupRoot` 或 `BETTERCM_BACKUP_ROOT` 指定其他存储位置。源码快照和完整 Git bundle 放在 `snapshots`，构建归档放在 `archives`。

```powershell
.\Maintenance.ps1 -BackupRoot D:\BetterCM-Storage
.\Maintenance.ps1 -Apply -BackupRoot D:\BetterCM-Storage -SkipQaArchive
# 迁出旧目录：只支持同卷原子移动，保留全部历史内容
.\Maintenance.ps1 -Apply -MigrateLegacyStorage -MigrateOnly
# 明确启用生成归档的数量/容量限制；先不加 Apply 查看计划
.\Maintenance.ps1 -PruneArchives -KeepArchiveRecent 12 -MaxArchiveGB 10 -SkipQaArchive
```

维护按类别保留最近 N 个构建目录，覆盖 dev、installer、QA compiler/installer、script-fixture 与 script-storage（共用保留预算）、baseline 和 maintenance。先生成源码快照/Git bundle，再归档构建目录、逐文件 SHA256 校验，最后移除已归档目录。发布载荷、QA 原目录及历史备份保留。

`BackupRoot` 必须在工作区之外，且不能是包含工作区的父目录。只有显式 `-PruneArchives` 才清理符合脚本命名规则的已有归档；清理计划固定在本轮开始时、在新归档创建前执行。本次新归档始终保留，包括数量设为 0 或新归档超过容量预算的情况，限额可能暂时超出，后续运行再评估。未知文件、`snapshots`、`legacy-backups` 和 `legacy-archives` 不参与清理。跨卷历史备份迁移需另行复制并校验，脚本拒绝把跨卷复制冒充原子移动。维护期间应停止其他构建。

本轮已把旧备份和旧归档共 61,435,463,137 字节迁到项目旁存储，文件数及总字节数一致。这减少项目树内容，没有释放磁盘空间。其余发布包和 QA 证据仍保留在工作区。

项目源码按 [MIT](LICENSE) 授权，依赖和图标来源见 [THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES.md)。历史过程见 [CHANGELOG](CHANGELOG.md) 和 [文档索引](docs/README.md)。

## 1.9.0 工作树复查修正（历史）

本轮补上解决方案配置、归档新副本保护、watcher/输入可见性、安装测试参数及断言、Clone 字段漂移回归。后续验证见 [复查修正记录](docs/releases/1.9.0/REVIEW-FIX-1.9.0.md)。此前候选包与首次 QA 记录保留为历史证据，不能用于证明本次修正。

隔离安装测试默认使用 Internal 范围，无需事先生成最终 Release 安装包：先运行完整 verify 生成 QA 发布目录，再编译 TestBuild，最后执行 `./Installer/Test-Installer.ps1 -Isolated`。若要同时核对最终包，可以显式加 `-ReleaseKind Release`。

## 第三轮首帧与失败夹具检查（1.9.0 历史）

首帧门禁不再用单次 250 ms 混合判断首次与稳定状态。NativeUi/C037 记录检查窗口首次打开及五次重复打开；首次样本要求 <1000 ms，稳定状态中位数 <250 ms、nearest-rank P95 <500 ms（五个样本时即最大值）。每个样本必须真的执行 Hold、完成呈现、查询到 DWM 已解除遮蔽，并有有限正耗时。未开动画或遮蔽失败会明确失败，不能用 0 ms 通过。此项测量从 Hold 到完成，未测量整个程序启动或 Shell 扩展读取时长。

脚本检查失败时遗留的 script-fixture/script-storage 共用一份维护预算，其归档都参与显式清理；本次新建归档仍受同轮保护。watcher 首次创建遭遇 IO/权限错误后也会按两秒间隔重试。最新范围见 [稳定性修正记录](docs/releases/1.9.0/STABILITY-FIX-1.9.0.md)。
