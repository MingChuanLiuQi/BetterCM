# BetterCM 0.5.0 项目交接

日期：2026-10-12。工作区：C:\Users\zky89\Desktop\文件夹\BetterCM。基线：HANDOFF-0.4.0.md、修改前完整备份。本轮按用户请求处理焦点/Mica、缺失背景命令及截图第 8–12 项工程任务。

## 本轮实现

菜单材质改用 Microsoft.WindowsAppSDK.InteractiveExperiences 的 MicaController(BaseAlt)/DesktopAcrylicController，SystemBackdropConfiguration.IsInputActive=true，短暂无法激活也保持材质。ThemeService 持有并释放 MenuBackdrop；控制器生效时 DWM backdrop=NONE，避免双重合成，设置窗口保留 DWM 材质。关闭透明效果、高对比度或不支持时保留回退。只引用 InteractiveExperiences 及其 Base 依赖，自带必要原生组件，没有引入完整 WinUI/AI SDK。

DesktopWindowTarget 负责父 HWND 材质，WPF Surface 放入透明子 HwndSource，否则材质会覆盖 WPF 内容。父窗口仍非分层、AllowsTransparency=false，系统圆角/阴影保留；子窗口使用逐像素透明并显式继承资源/文字颜色、转发 PreviewKeyDown。MeasureContent 先 UpdateLayout/InvalidateMeasure，再测量、安排及同步尺寸；Reposition 使用测量的 Width/Height，避免 ActualHeight 延迟更新导致展开裁切。显示期间用 _opening 防旧焦点事件；实际检查 Activate/GetForegroundWindow，失败只在原来源/菜单仍为前台时重试一次。

ShellViewBinding 通过 Shell.Application.Windows 匹配原 Explorer HWND，QueryService(SID_STopLevelBrowser, IShellBrowser) → QueryActiveShellView。桌面使用 FindWindowSW 的真实桌面命名空间视图；背景菜单 GetItemObject(SVGIO_BACKGROUND, IContextMenu)，不再优先使用物理 Desktop 文件夹对象菜单。不可用时才回退原 CreateViewObject/GetUIObjectOf。读取/执行前 IFolderView/IPersistFolder2 检查位置，变化则拒绝旧视图操作。桌面刷新、查看、排序恢复；refresh 默认一级、支持 F5，在独立 ShellBridge 连接执行 IShellView.Refresh，无模拟 F5。实际原生 refresh 可能没有 verb，MenuComposer 增加中文/英文精确标题去重，第三方分组与关闭内建动作时保留替代项。

Tests/BetterCM.Tests.csproj 为 xUnit，81 项逻辑检查通过 dotnet test，无 GUI/Shell COM 依赖。LogicChecks/RegressionFixtures 提供共享逻辑与夹具，SelfTests 仅保留 UI/Shell/剪贴板格式，失败返回非零。global.json 固定 10.0.401（latestPatch）；verify.ps1 实施 build -warnaserror → test → self-contained publish -warnaserror → --self-test。GitHub Actions 调用同一脚本，远端尚未运行（当前无 Git 仓库/远端）。

唯一版本来源是 BetterCM.csproj Version=0.5.0。AppInfo 读取 AssemblyInformationalVersion，程序集、manifest、UI、托盘、报告和安装包派生同一版本。MainWindow.xaml 每元素独立行；Win32 钩子、消息、DWM、菜单/窗口/系统参数标志改具名常量。Build-Installer.ps1 先验证再编译到唯一 artifacts staging 目录，成功后复制正式包到根目录并生成 SHA256，避免旧输出路径 EndUpdateResource(110) 干扰。

## 交付

- 正式包：BetterCM-0.5.0-Setup-x64.exe，61,120,652 字节，0.5.0.0，自带运行时与材质组件。
- SHA256：DF260BFF20B869A99DE1E368351D40354BD1FD2A790256417C35B80EC6357E2F。
- 修改前归档：Backups/BetterCM-0.4.0-before-0.5.0-20261012-100631.zip，1143 文件全部核验，525,024,172 字节。
- 归档 SHA256：5B4FE3D71212F9F204F983EC34D3EF9554D7EA5FB6E01C5D474775A6F62C6D17；有逐文件 .manifest.json 与 .zip.sha256。
- 实际安装仍为 0.4.0，已恢复后台运行；用户可双击新包升级，未自动覆盖。原配置、自启项保持不变，测试安装已卸载，旧包/历史备份保留。

## 最终验证

零警告/零错误构建与发布；xUnit 81/81；发布及隔离安装 UI/Shell 各 72/72；压力 58/58；真实输入 2/2；隔离安装/卸载 13/13。

最终实机 Explorer 背景导入 46 个 Shell 叶子；更多中查看/排序/分组依据正常，刷新入口仅一个，两次右键 NativePopups=0。明确未激活预览保持 Mica 与文字，同时验证 Ctrl+F 真聚焦、空提示消失、输入筛选、展开与缩回；真实桌面刷新在自动自检执行。

压力实测第二次 open 37.72 ms，50 次关联请求 p50 0.26 ms/最大 0.80 ms，捕获超时 1809.80 ms、后续新捕获 217.09 ms。执行超时和 Dispose 不 Kill 执行宿主，保留原安全原则。不是 50 次实体桌面连击。

完整记录 QA.md / QA-0.5.0.md，历史 QA-0.4.0.md。最终报告：artifacts/qa/headless/headless-tests.trx、published-tests.json、installed-tests.json、published-stress-0.5.0.json、input-0.5.0.json、installer-smoke.json、live-explorer-0.5.0.json、live-ui-0.5.0.json、environment-restored-0.5.0.json。

## 后续维护

新增关键文件：Services/MenuBackdrop.cs、ShellViewBinding.cs、MenuFocusPolicy.cs、AppInfo.cs、LogicChecks.cs；Tests 项目、global.json、verify.ps1、.github/workflows/verify.yml。

主变化：ContextMenuWindow.xaml.cs、ThemeService、ShellMenuSession、ShellBridge、ActionExecutor、ActionCatalog、MenuComposer、SelfTests/MenuUiChecks；App、MainWindow、NativeMethods、安装脚本和 manifest。SourceWindow 的 JSON/进程传输仍沿用显式协议参数，不能把纯路径动作用于虚拟目标。

多标签、混合 DPI、多显示器、远程桌面及所有 namespace/扩展组合还需要更广设备矩阵。背景回退接口和只提供新式 IExplorerCommand 的扩展存在原有范围边界；Shift+右键保留系统菜单。不要移除执行超时不 Kill 的原则，不要将材质常活动策略扩大到后台设置窗口。
