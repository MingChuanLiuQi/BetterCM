# BetterCM 0.6.0 项目交接

日期：2026-10-12。工作区：C:\Users\zky89\Desktop\文件夹\BetterCM。基线 HANDOFF-0.5.0.md。按用户本轮要求完成右键泄漏根因修复、更多/分组动效和截图六项改进；保留现有 UI 技术栈及独立 Shell 宿主。

## 关键实现

InputService 不再根据拖动阈值重放右键按下，也不让普通右键移动转为原生手势。RightClickGesture 固定完整按下/松开与原始位置，已接管松开先处理，通知失败仍返回吞掉；焦点/配置/位置变化不更改归属。缓存 popup HWND，关闭动画期间仍吞掉其内部成对右键，避免松开落到 Explorer。Progman/WorkerW 直接作为桌面范围，Explorer 文件视图/导航树保持识别。原生菜单/原生右键拖动改为按下前 Shift 的明确入口，没有注入、补发或全局模拟输入。

ExpansionAnimator 用有限 CompositionTarget.Rendering 订阅驱动非分层父窗口高度。透明 WPF 子窗口在开始时设置最终尺寸，父窗口裁切揭示，避免逐帧 UpdateLayeredWindow 导致旧位图覆盖最终大小。MoreExpansionAnimation/GroupExpansionAnimation 与对应时长独立，默认 fade-slide、220/180 ms；EnableExpansionAnimations 与全局 EnableAnimations、系统动画/高对比度共同生效。新行淡入/滑入，已有行不重演；搜索、重开、关闭取消旧帧与行动画。动画中不查询 Shell、不调用 Compose。

ContextMenuWindow 按 Auto 行实测头部/状态/底栏、按每种行类型各测一个模板得到自然总高，再约束 Surface.MaxHeight 和工作区；移除 MaxHeight-210。虚拟列表的 DesiredSize 仅是当前视口，不可作为总高。原生尺寸统一驱动，不能同时逐帧写 WPF Window.Height；子窗口不能逐帧跟着 resize，否则其分层位图可能最终仍旧尺寸。Surface.Height 明确匹配最终布局，窗口帧只裁切。已有 Mica 活动态与资源/键盘转发保留。

MenuEntry Origin/IsShell 区分内置、原生及具名分组来源；未知供应者只标 Shell。分组行有背景/左侧标记，来源标签不影响 ShellKey/Action/Session。SearchPlaceholder 为非交互 Adorner，焦点/IME 时隐藏，保留标准 TextBox 输入。

Hotkey.Parse 拒绝空段、重复修饰键；MainWindow 编辑即反馈并禁用保存，占用仍在保存注册时反馈。“还原为已保存”取消表格编辑/预览并载入已保存配置，不写配置/自启项。新配置字段有默认值，旧配置无需迁移脚本。MainWindow.xaml 保持逐元素格式化。

## 验证

最终零警告/零错误构建发布；xUnit 97/97；发布/隔离安装各 87/87 UI/Shell/设置检查；压力 58/58；最终真实输入 2/2；最终安装/卸载 13/13。

新增覆盖 10000 次手势配对、原生/菜单内部手势及重复释放、空/重复快捷键、新字段迁移/校验、来源身份、有限动画结束与取消、长列表实际窗口/内容高度、多行状态、即时校验和还原不写配置。10000 次为状态机，不是实体点击。

实机最终输入实现六次 Explorer 背景右键 AcceptedClicks=6/NativePopups=0。实机更多、NanaZip 原位分组、来源、搜索聚焦/SHA 筛选与缩回正常。最终长菜单父/子原生窗口均 960 px（640 DIP）、底栏在窗口内。不要把六次操作及 50 次 IPC 关联压力宣称为所有速度/硬件的实体连击验证。

压力：第二次 open 38.09 ms，50 次 p50 0.69 ms/最大 2.14 ms，捕获超时 1803.26 ms、后续 228.67 ms。最后改动仅为 QA 布局快照空 HWND 的 Created=false 回退；所测核心实现相同，最终包另通过自检、安装及真实输入。

报告：artifacts/qa/headless/headless-tests.trx、published-tests.json、installed-tests.json、published-stress-0.6.0.json、input-0.6.0.json、installer-smoke.json、live-preview-0.6.0.json、live-right-click-0.6.0.json、environment-restored-0.6.0.json。完整范围 QA.md / QA-0.6.0.md，历史 QA-0.5.0.md。

## 交付与恢复

- 正式包 BetterCM-0.6.0-Setup-x64.exe，61,140,099 字节，版本 0.6.0.0，自带运行时/材质组件。
- SHA256：95CF2C408102A1EE2BE49B2B37A1115DC65A533EAF8FA19E1CE77680EA9C1E09。
- 修改前完整归档 Backups/BetterCM-0.5.0-before-0.6.0-20261012-111830.zip：2799 文件全部验证，1,154,102,768 字节。
- 归档 SHA256：C91E539791F3F4BD56DCDE4315421F7FB1E1D9255C8F17A9E84F3A116E44031F；同目录有清单及校验文件。
- 现有正式安装仍为 0.5.0，已恢复后台；原配置、自启项未变。用户双击新包升级。测试安装已卸载，测试窗口清理，旧包与备份保留。

新增主要文件 Services/RightClickGesture.cs、ExpansionAnimator.cs、SettingsUiChecks.cs；Views/SearchPlaceholder.cs。主变化 InputService、ContextMenuWindow.xaml/.cs、MenuComposer/MenuEntry、MainWindow、Config/AppConfig、config.json、ThemeService、LogicChecks/MenuUiChecks/SelfTests、App 的显式 QA 观察。

后续保持执行超时不 Kill 宿主/子进程的原则，维持 classic IContextMenu 范围与 Shift 回退。不要为了 UI 动效让 COM 进入输入回调或帧回调。远端 CI 本轮未运行，本地无 Git 仓库/远端；更广设备/IME/namespace/扩展矩阵仍待验证。
