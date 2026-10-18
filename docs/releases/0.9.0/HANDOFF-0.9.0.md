# BetterCM 0.9.0 项目交接

工作区 `C:\Users\zky89\Desktop\文件夹\BetterCM`，基线 HANDOFF-0.8.0.md。用户要求先备份，默认忠实换皮经典菜单、修复开启动画终点闪动及新建后的原生名称编辑。代码、测试、打包和环境恢复均完成。目录没有 Git 仓库，远端 CI 未运行。

## 交付状态

正式包 `BetterCM-0.9.0-Setup-x64.exe`：61,171,838 字节，SHA256 `D3987CEACE09BD7C51DBE84220A291D92330E6DA63200AA29AF3CC82391AA3AE`。最终发布目录 artifacts/publish/win-x64。修改前备份 `Backups/BetterCM-0.8.0-before-0.9.0-20261013-024440.zip`，3165 文件逐一核对通过，SHA256 `07E3D3077DEC4FF84B90350FE41FF9947EA6B5BDA9D35CB38E64E6DFD1B393A5`。历史安装包和备份保留。

构建 0 警告/错误，119 xUnit，发布和隔离安装各 116 UI/Shell 检查，压力 58，安装卸载 13，外部输入 2，侧边输入 3，全通过。详见 QA-0.9.0.md。真实安装 0.8.0.0 已恢复后台，用户配置和启动项不变，隔离安装已卸载；不要声称自动安装了 0.9.0。

版本唯一来源 BetterCM.csproj Version=0.9.0，程序集/文件版本 0.9.0.0，AppInfo 从程序集读取。后续源码修改需要重新验证和打包，不复用中间安装包哈希。

## 默认原生树

AppConfig.ClassicMenu 默认 true；旧配置缺字段也默认经典模式。App.ShowMenuAsync 在此模式不构建可见内置/custom 动作，等待当前 ShellResponse 后以最终尺寸首次显示。全部一级行直接展示，旧隐藏/固定/更多设置不重排系统树；MainWindow 经典开关禁用增强编辑，手动关闭后才恢复旧增强模式。

ShellMenuSession.ReadMenu 输出原 HMENU 树 ShellMenuNode 和平面 ShellItem 索引；保留顺序、分隔线、组名、启用/勾选/默认、快捷键和助记键，递归 WM_INITMENUPOPUP 初始化 IContextMenu2/3 子菜单。ShellBridge 协议增加 Menu。NativeMenuComposer 只映射叶子委托，不重组第三方项目，搜索保留层级。

Home 实际存在重复命令 ID 79，平面字典须 DistinctBy Id；每条视觉行仍保留自己的标题、启用状态和唯一路径 ID，不能按标题去重。动作 ID 为 native:<number>，视觉行 ID 独立。背景辅助粘贴仍用 0x10000 路由，只替换命令身份和启用状态，不改树位置。父禁用状态向下传播。

MenuEntry 扩展 IsNativeLayout/Checked/IsDefault/GroupEnabled/Mnemonic，经典行无来源徽标、强调线或固定按钮，默认项粗体，勾选显示标记。ListBoxItem.IsEnabled 绑定有效性，键盘跳过禁用和分隔线。紧凑模式保留分组、关闭搜索，子菜单沿用侧边窗口链。

QQ 音乐确实注册了经典文件夹菜单项目，键为 HKCR\Directory\shell\QQMusic.1.Play / QQMusic.2.Add。本轮保留原生结果，不能误认 BetterCM 原创动作而过滤。

## Home 导航绑定

SelectedContext.PinnedNavigation 经捕获和 Shell 请求传递。UIA 可能省略“已固定”，顶层 names.Length==2 用当前 Home.Items 的 System.Home.IsPinned 补充；桌面根和深层节点不走该回退，不缓存固定顺序。

固定项绑定 Home 根 F874310E-B6B7-47DC-BC84-B9E6B38F5903 的当前 child PIDL。Home.ParseDisplayName(物理路径) 本机 E_INVALIDARG，不能改回。IEnumIDList 按 0x60 枚举，ILCombine(home,child)，SHGetNameFromIDList(SIGDN_FILESYSPATH) 比较路径，再对 Home child 获取菜单，才保留原生取消固定。本轮未改变用户固定列表。

## 原生新建和宿主

ShellMenuSite 实现 IServiceProvider / INewMenuClient 适配，仅绑定实际 Explorer 背景视图时给原 context 附加 site。保留旧 site，NewMenuClient 服务由本站处理，其他服务转发原 provider/Explorer。ComServiceReference 用 COM Global Interface Table 在调用者 apartment 取得服务代理，不在 RPC 回调线程直接使用原 STA 对象。

SelectAndEditItem 回调给出真实绝对 PIDL，ILClone 后派发原 STA，验证视图位置再选择/编辑。ILCloneFull 是 SDK 宏/内联，不是 shell32 导出，P/Invoke 要用 ILClone。IShellView UIActivate 槽 7 / 状态 2，SelectItem 槽 14，edit flags=0x1F，select flags=0x1D；实测原生先 flags 0 再 flags 3。没有轮询文件、猜文件名或模拟 F2。

Invoke 后 WaitForNewCallbackAsync（编辑完成或最多 3 秒宽限），再 ReleaseCommands 还原 site、销毁 HMENU、释放 context/folder/辅助会话，再 ShellHostLifetime.DrainAsync（600ms、线程引用及可见原生 UI），最后 DrainCallbacks、Dispose view/site。不要先等线程引用归零再释放自己的 context，这会造成宿主不退出。已 Dispose 的晚到回调返回 CO_E_OBJNOTCONNECTED。

文件夹新建及属性实机使用 dev-key 构建，其 DLL 与最终发布 SHA256 相同：32C365386E25D18C8C94C0BB0F998DBE90D998B1F5C86F10FA2CAB437F26797B。文本新建在较早开发构建验证。截图和报告在 artifacts/qa。

## 动画、焦点与键盘

SurfaceAnimator 开启结束保留 HoldEnd 时钟，只在隐藏/复用移除，先设最终基值避免闪回；关闭恢复 X/Y 两方向。主 HWND 不动画 Opacity。ThemeService 菜单 SourceInitialized 设置材质，不在 ContentRendered 重建。DWM 属性 3 DWMWA_TRANSITIONS_FORCEDISABLED 禁用额外系统过渡，NativeTransitionResult 保存设置 HRESULT；GetWindowAttribute 对该属性不可靠，不用读回值验收。

Mica 属父 HWND，WPF 键盘属透明内容子 HWND。Activated / WM_SETFOCUS 排队 FocusContent，只在菜单仍打开且已为前台时 SetFocus 内容 HWND，必要时 Focus ActionsList；不能夺取外部窗口焦点。保留 MenuFocusPolicy 有界重试，不宣称消除全部 Windows 前台限制。

根 Window/Surface 禁用 InputMethod.IsInputMethodEnabled，SearchBox 显式启用，防止拼音吞访问键；非搜索 Key.ImeProcessed 用 ImeProcessedKey。助记键来自原 & 标记，也兼容 Explorer 去掉 & 后留下的 (W)/（W）；&& 为文字。重复字母循环，唯一组打开，唯一叶子执行。F5 刷新原视图，Alt+Enter 等绑定原命令。实测验证的是菜单激活后的 W / Alt+Enter。

BETTERCM_QA_REPORT 才启用 .keys.jsonl/.new.jsonl 和快照，正常运行关闭诊断。历史日志含中间失败，不可整体当作最终结果。

## 构建和边界

verify.ps1 执行警告视为错误的构建、xUnit、发布和 --self-test；Build-Installer.ps1 后接 Inno 打包。csproj 排除 Tests/Backups/artifacts 的 Compile/Content/Resource/None 和默认扫描，防 QA PNG 被产品误收及测试复制失败，不能删掉排除。

新增 Tests/NativeMenuTests.cs、Services/NativeUiChecks.cs，更新 SelfTests/MenuUiChecks/SettingsUiChecks。动画检查采集实际渲染帧及结束后至少 180ms 状态。--input-check / --cascade-input-check 是真实输入夹具。压力检查后最终只调焦点/IME，IPC 未变；最终发布和安装自检重新执行。

继续用 WPF、独立 ShellHost、经典 IContextMenu2/3。无文字自绘、仅新式接口扩展及 Explorer 自加的导航控制项有接口边界；Shift+右键保留系统入口。未清空用户回收站，真实复制/移动仅专用夹具。单机测试不替代混合 DPI、多标签、远程桌面或全扩展矩阵。安装包未代码签名。

artifacts/dev*-0.9.0 为中间开发输出，不用于交付；正式输出仅 artifacts/publish/win-x64 和根正式安装包。完整结果、截图及环境恢复见 QA-0.9.0.md。
