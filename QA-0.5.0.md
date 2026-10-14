# BetterCM 0.5.0 验证记录

日期：2026-10-12。环境：Windows 11 x64 build 26300，150% 缩放，.NET SDK 10.0.401。

## 修改前备份

修改源码之前创建完整 0.4.0 归档 Backups/BetterCM-0.4.0-before-0.5.0-20261012-100631.zip，包含 1143 个文件，涵盖源码、资源、文档、历史安装包、bin/obj/artifacts，排除 Backups 自身。归档大小 525,024,172 字节。归档内全部文件与独立 .manifest.json 逐一 SHA256 校验通过。

归档 SHA256：5B4FE3D71212F9F204F983EC34D3EF9554D7EA5FB6E01C5D474775A6F62C6D17。同目录保留 .zip.sha256。

## 自动验证

最终源码经 verify.ps1 的 build -warnaserror → dotnet test → self-contained publish -warnaserror → --self-test 验证链通过，构建与发布零警告、零错误。

| 范围 | 结果 | 报告 |
| --- | --- | --- |
| 无界面 xUnit 逻辑检查 | 81/81 | artifacts/qa/headless/headless-tests.trx |
| 最终发布目录 WPF/UI/Shell 检查 | 72/72 | artifacts/qa/published-tests.json |
| 隔离安装目录 WPF/UI/Shell 检查 | 72/72 | artifacts/qa/installed-tests.json |
| 最终发布目录慢命令/捕获压力 | 58/58 | artifacts/qa/published-stress-0.5.0.json |
| 最终发布目录真实输入检查 | 2/2 | artifacts/qa/input-0.5.0.json |
| 隔离安装、负载、启动、退出及卸载 | 13/13 | artifacts/qa/installer-smoke.json |

Tests/BetterCM.Tests.csproj 不创建 Application、窗口或 Shell COM 会话，dotnet test 可无界面运行。原有纯逻辑检查迁入 LogicChecks，覆盖参数、多选、配置、哈希、MenuComposer、关闭策略与新增焦点/刷新规则。--self-test 保留需 WPF/剪贴板数据格式/真实 Shell 的部分，复用 RegressionFixtures；失败返回非零退出码。

本轮新增真实控件检查覆盖材质与透明内容子窗口归属、文字颜色继承、焦点无关的 Mica/Acrylic 策略、展开后原生高度同步、真实桌面刷新及背景分组。150% 缩放下布局回归记录：折叠 169.77 DIP，展开 267.33 DIP，原生窗口同步为 401 px。原有空搜索提示、分组、动画代次、悬停延时、纯色回退与深浅主题检查继续通过。

GitHub Actions 已配置调用同一 verify.ps1，global.json 固定 10.0.401 并允许同补丁带更新。工作区无 Git 远端；本轮只执行本地验证，远端 CI 尚未运行。UI/Shell 自检需要 Windows 桌面环境，不能将其视为纯无界面测试。

## 实机输入与视觉核对

通过 computer-use / @oai/sky 操作本机，未注入 Explorer DLL，未以脚本合成全局输入。

- 明确以 --preview-inactive 显示未激活菜单，实机截图同时显示 Mica Alt、正常文字、图标、按钮和底栏。随后激活、Ctrl+F 聚焦搜索、输入 SHA，真实文本筛选正常；空搜索框聚焦只显示光标。
- 展开更多后窗口随内容增高，底栏仍可见；筛选到单个动作后窗口随内容缩回。
- 最终发布负载的资源管理器背景右键正常显示一级刷新/F5，导入 46 个 Shell 叶子项目。更多项目包含查看、排序方式、分组依据等真实视图分组；最终可访问性记录仅有一个刷新按钮。记录见 artifacts/qa/live-explorer-0.5.0.json 与 live-ui-0.5.0.json。
- 最终实机观察的两次接管右键 NativePopups=0。较早实机检查使用 F5 执行原 Explorer 视图刷新；自动自检另检查真实桌面 IShellView.Refresh 成功。
- 最终发布程序的真实输入夹具 2/2 通过：未激活菜单收到外部左键后关闭，原按钮收到点击；另一窗口成为前台后菜单关闭，保留 70 ms 关闭动画。

视觉检查发现并修复了组件检查起初未覆盖的问题：DesktopWindowTarget 与 WPF 共用同一 HWND 时背景覆盖文字；改为父窗口原生材质、透明子 HwndSource 呈现内容。随后 Ctrl+F 实机检查发现展开时沿用旧窗口高度导致裁切；修复内部布局刷新、重新测量与同步原生尺寸，并增加回归。实际背景视图还会省略刷新 canonical verb，已增加中文/英文精确标题回退去重，关闭内建动作时仍保留原生刷新，第三方分组不误合并。

## 慢命令及捕获压力

最终发布负载 58/58 通过。测试使用独立假 Shell 宿主的 40 秒 STA verb，不执行用户文件动作。

| 检查 | 本次实测 |
| --- | --- |
| 40 秒 invoke 期间第二次 open | 37.72 ms |
| 50 次连续 open | 50/50 正确；p50 0.26 ms，最大 0.80 ms |
| invoke 30 秒等待超时 | 返回通信失败，执行宿主仍存活 |
| bridge Dispose | 不结束执行宿主 |
| verb 完成后 | 宿主自行退出 |
| 卡住捕获超时降级 | 1809.80 ms |
| 后续新捕获 | 217.09 ms |

这些数字只衡量通信和隔离夹具，不代表所有真实 Shell 扩展、界面帧时间或实体连续右键的性能。未把 50 次通信请求宣称为 50 次实体桌面连击。

## 安装包及现有安装保护

项目根目录正式包 BetterCM-0.5.0-Setup-x64.exe：61,120,652 字节，文件版本 0.5.0.0。

SHA256：DF260BFF20B869A99DE1E368351D40354BD1FD2A790256417C35B80EC6357E2F。根目录附同名 .sha256。

安装负载包含 .NET/WPF、自带 Windows App SDK InteractiveExperiences/Base 材质组件与对应许可；不依赖机器上的全局 .NET。正式发布目录共 503 个文件、222,801,341 字节，包括运行时本地化资源；没有 xUnit、testhost 或测试项目产物。BetterCM.exe SHA256：D57CBD8BFDA9802E1DC8C7D98A3DDB16E4BB94516C5B62ACC60407C2813BDB5C。

隔离 TestBuild 使用独立 AppId、启动项和数据目录，安装全部负载逐文件 SHA256 与最终发布目录一致；安装、72 项自检、后台运行、单实例、退出及卸载均通过，隔离安装已卸载。正式包和隔离包包装身份不同，程序负载相同。

本机直接向旧输出文件名编译时两次发生 Inno EndUpdateResource(110) 错误；换用新的独立 staging 输出目录编译后成功，再复制完整包到根目录。Build-Installer.ps1 已采用此方式，不改动系统安全设置。

实机右键检查期间临时退出实际 0.4.0，使用独立 BETTERCM_DATA_DIR，完成后恢复原版本后台运行。原用户配置 SHA256 与自启项保持不变；没有覆盖实际正式安装。基线见 artifacts/qa/ui-baseline-0.5.0.json，最终保护核对见 environment-restored-0.5.0.json。

## 验证范围

当前设备为 Windows 11 x64 build 26300、150% 缩放。混合 DPI、多显示器、远程桌面及所有第三方 Shell 扩展尚未逐一验证；系统材质仍遵从透明效果和高对比度设置。Shell 导入继续基于经典 IContextMenu，接口不支持的扩展可用 Shift+右键。安装包保留原有 AppId 和用户配置兼容性。0.4.0 历史记录见 QA-0.4.0.md。
